namespace Noye.Modules {
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using AngleSharp.Parser.Html;

    public class Naver : Module {
        private readonly Regex NAVER_RE = new Regex("<meta property=\"og:title\" content=\"(.*?)\"",
            RegexOptions.Compiled | RegexOptions.Multiline); // pending changes

        private readonly Regex VLIVE_RE = new Regex("<meta property=\"og:title\" content=\"(.*?)\"",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public Naver(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(@"vlive.*\/(?<id>\d+)[\/|\?]?", async env => {
                foreach (var id in env.Matches.Get("id")) {
                    var vplus = await IsVPlus(id);
                    var title = await GetTitle("http://www.vlive.tv/video/" + id, VLIVE_RE);

                    var sb = new StringBuilder();
                    if (vplus) {
                        sb.Append("[V+] ");
                    }

                    sb.Append(title);
                    await Noye.Say(env,  sb.ToString());
                }
            });

            Noye.Passive(@"tv\.naver\.com\/v\/(?<id>\d+)\/?", async env => {
                foreach (var id in env.Matches.Get("id")) {
                    var title = await GetTitle("http://tv.naver.com/v/" + id, NAVER_RE);
                    await Noye.Say(env, title);
                }
            });
        }

        private async Task<bool> IsVPlus(string id) {
            var resp = await httpClient.GetStringAsync("http://m.vlive.tv/video/" + id);
            var parser = new HtmlParser();
            var dom = parser.Parse(resp);
            var el = dom.QuerySelector("#wrap.vproduct");
            return el != null;
        }

        private async Task<string> GetTitle(string url, Regex re) {
            var stream = await httpClient.GetStreamAsync(url);
            var buf = new byte[4 * 1024];
            await stream.ReadAsync(buf, 0, buf.Length);

            var match = re.Match(Encoding.UTF8.GetString(buf));
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }
}