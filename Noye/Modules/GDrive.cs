namespace Noye.Modules {
    using System;
    using System.Text;
    using System.Threading.Tasks;

    public class GDrive : Module {
        private readonly string apiKey;

        public GDrive(INoye noye) : base(noye) {
            apiKey = ApiKeyConfig.Get<GDriveConfig>();
        }

        public override void Register() {
            Noye.Passive(@".*?drive\.google\.com\/.*?\/(?<id>[A-Za-z0-9_-]{33})\/?", async env => {
                await env.TryEach("id", WithContext(env, "link was empty"), async (id, ctx) => {
                    var link = await LookupLink(id);
                    await Noye.Say(env, link, ctx);
                });
            });

            Noye.Passive(@".*?drive\.google\.com\/uc.+?id=(?<id>[A-Za-z0-9_-]{33})&?", async env => {
                await env.TryEach("id", WithContext(env, "link was empty"), async (id, ctx) => {
                    var link = await LookupLink(id);
                    await Noye.Say(env, link, ctx);
                });
            });
        }

        private async Task<string> LookupLink(string id) {
            var url = $"https://www.googleapis.com/drive/v2/files/{id}?key={apiKey}";
            var resp = await httpClient.GetAnonymous(url, new {
                title = default(string),
                fileSize = default(long),
                videoMediaMetadata = new {
                    width = default(int),
                    height = default(int),
                    durationMillis = default(long)
                }
            });

            if (resp == null) {
                return null;
            }

            var sb = new StringBuilder();
            sb.Append($"[{resp.fileSize.AsFileSize()}] {resp.title}");
            if (resp.videoMediaMetadata != null) {
                var time = TimeSpan.FromMilliseconds(resp.videoMediaMetadata.durationMillis).StripMilliseconds();
                sb.Append(
                    $" | {resp.videoMediaMetadata.width}x{resp.videoMediaMetadata.height} · {time.AsShortTime()}");
            }

            return sb.ToString();
        }
    }
}