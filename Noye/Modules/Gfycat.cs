namespace Noye.Modules {
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public class Gfycat : Module {
        public Gfycat(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(@"(?:gfycat\.com\/(?<id>.+?)\b)", async env => {
                foreach (var id in env.Matches.Get("id")) {
                    await LookupItem(env, id);
                }
            });
        }

        private async Task LookupItem(Envelope env, string id) {
            var json = await httpClient.GetStringAsync($"http://gfycat.com/cajax/get/{id}");
            var item = JsonConvert.DeserializeAnonymousType(json, new {
                gfyItem = new {
                    title = default(string),
                    description = default(string),
                    url = default(string),
                    views = default(long),
                    webmSize = default(long),
                    width = default(long),
                    height = default(long),
                    nsfw = default(string)
                }
            })?.gfyItem;
            if (item == null) {
                return;
            }

            var sb = new StringBuilder();
            if (item.nsfw == "1") {
                sb.Append("[NSFW] ");
            }

            var has_title = !string.IsNullOrWhiteSpace(item.title);
            if (has_title) {
                sb.Append($"{item.title} ");
                if (!string.IsNullOrWhiteSpace(item.description)) {
                    sb.Append($"· {item.description} ");
                }
            }

            if (!string.IsNullOrWhiteSpace(item.url)) {
                if (has_title) {
                    sb.Append("| ");
                }

                sb.Append($"{item.url} ");
            }

            if (has_title) {
                sb.Append("(");
            }

            sb.Append($"{item.width}x{item.height}, {item.webmSize.AsFileSize()}");
            if (has_title) {
                sb.Append(")");
            }

            await Noye.Say(env, sb.ToString());
        }
    }
}