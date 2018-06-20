namespace Noye.Modules {
    using System.Text;
    using System.Threading.Tasks;
    using AngleSharp.Extensions;
    using AngleSharp.Parser.Html;

    public class Imgur : Module {
        public Imgur(INoye noye) : base(noye) {
            var key = ApiKeyConfig.Get<ImgurConfig>();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Client-ID {key}");
        }

        public override void Register() {
            Noye.Passive(@"imgur\.com/a/(?<id>.*?(:?\s|$))", async env => {
                await env.TryEach("id", WithContext(env, "cannot lookup imgur album"), async (id, ctx) => {
                    var album = await LookupAlbum(id);
                    await Noye.Say(env, album, ctx);
                });
            });

            Noye.Passive(@"i\.imgur\.com\/(?<id>.+?)\.(:?jpg|jpeg|gif|gifv|png)", async env => {
                await env.TryEach("id", WithContext(env, "cannot lookup imgur image"), async (id, ctx) => {
                    var image = await LookupImage(id);
                    await Noye.Say(env, image, ctx);
                });
            });
        }

        private async Task<string> LookupImage(string id) {
            var body = await httpClient.GetStringAsync($"https://imgur.com/{id}");

            var parser = new HtmlParser();
            var doc = parser.Parse(body);
            var name = doc.QuerySelector("h1.post-title").Text();

            var json = await httpClient.GetAnonymous($"https://api.imgur.com/3/image/{id}", new {
                data = new {
                    title = default(string),
                    section = default(string)
                }
            });
            if (json?.data == null) {
                return null;
            }

            var album = json.data;
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(album.title)) {
                return null;
            }

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(album.title)) {
                sb.Append($"{album.title}");
            }
            else if (!string.IsNullOrWhiteSpace(name)) {
                sb.Append($"{name}");
            }

            if (!string.IsNullOrWhiteSpace(album.section)) {
                sb.Append($" ({album.section})");
            }

            return sb.ToString();
        }

        private async Task<string> LookupAlbum(string id) {
            var json = await httpClient.GetAnonymous($"https://api.imgur.com/3/album/{id}", new {
                data = new {
                    title = default(string),
                    description = default(string),
                    images_count = default(long),
                    views = default(long),
                    datetime = default(long),
                    nsfw = default(bool?),
                    section = default(string)
                }
            });
            if (json?.data == null) {
                return null;
            }

            var album = json.data;

            if ((string.IsNullOrWhiteSpace(album.title) || string.IsNullOrWhiteSpace(album.description)) &&
                album.images_count == 1) {
                return null;
            }

            var sb = new StringBuilder();
            if (album.nsfw.GetValueOrDefault(false)) {
                sb.Append("[NSFW] ");
            }

            if (!string.IsNullOrWhiteSpace(album.title)) {
                sb.Append($"{album.title} | ");
            }

            if (!string.IsNullOrWhiteSpace(album.description)) {
                sb.Append($"{album.description} · ");
            }

            var images = album.images_count.WithCommas();
            var views = album.views.WithCommas();
            sb.Append($"{images} images, {views} views ");

            if (!string.IsNullOrWhiteSpace(album.section)) {
                sb.Append($"({album.section})");
            }

            return sb.ToString();
        }
    }
}