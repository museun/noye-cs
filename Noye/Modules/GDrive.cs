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
                foreach (var id in env.Matches.Get("id")) {
                    await LookupLink(env, id);
                }
            });

            Noye.Passive(@".*?drive\.google\.com\/uc.+?id=(?<id>[A-Za-z0-9_-]{33})&?", async env => {
                foreach (var id in env.Matches.Get("id")) {
                    await LookupLink(env, id);
                }
            });
        }

        private async Task LookupLink(Envelope env, string id) {
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
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"[{resp.fileSize.AsFileSize()}] {resp.title}");
            if (resp.videoMediaMetadata != null) {
                var time = TimeSpan.FromMilliseconds(resp.videoMediaMetadata.durationMillis).StripMilliseconds();
                sb.Append(
                    $" | {resp.videoMediaMetadata.width}x{resp.videoMediaMetadata.height} · {time.AsShortTime()}");
            }

            await Noye.Say(env, sb.ToString());
        }
    }
}