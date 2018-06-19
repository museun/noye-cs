namespace Noye.Modules {
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using AngleSharp.Extensions;
    using AngleSharp.Parser.Html;

    public class Naver : Module {
        private readonly Regex NAVER_RE = new Regex("<meta property=\"og:title\" content=\"(.*?)\"",
            RegexOptions.Compiled | RegexOptions.Multiline); // pending changes

        private readonly Regex VLIVE_RE = new Regex("<meta property=\"og:title\" content=\"(.*?)\"",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public Naver(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(@"vlive\.tv\/(.*?)\/(?<id>\d+)\/?", async env => {
                foreach (var id in env.Matches.Get("id")) {
                    var vlive = await GetVLiveInfo(id);
                    await Noye.Say(env, vlive.ToString());
                }
            });

            Noye.Passive(@"tv\.naver\.com\/v\/(?<id>\d+)\/?", async env => {
                foreach (var id in env.Matches.Get("id")) {
                    var title = await GetTitle("http://tv.naver.com/v/" + id, NAVER_RE);
                    await Noye.Say(env, title);
                }
            });
        }

        private async Task<VLiveInfo> GetVLiveInfo(string id) {
            var resp = await httpClient.GetStringAsync("http://m.vlive.tv/video/" + id);
            var parser = new HtmlParser();
            var dom = parser.Parse(resp);
            var vplus = dom.QuerySelector("#wrap.vproduct") != null || dom.QuerySelector("div.vplay_chplus") != null;

            if (!vplus) {
                return new VLiveInfo {
                    VPlus = false,
                    Title = dom.QuerySelector("div.vplay_info > a.tit").Text().Trim(),
                    Views = dom.QuerySelector("span.icon_play > span.tx").Text().Trim(),
                    UploadAt = dom.QuerySelector("div.noti > span.txt").Text().Trim()
                };
            }

            var info = new VLiveInfo {
                VPlus = true,
                Title = dom.QuerySelector("div.pcard_large_txt_box > h2")?.Text().Trim(),
                Duration = dom.QuerySelector("div.pcard_large_sub_txt > span:nth-child(1)")?.Text().Trim(),
                UploadAt = dom.QuerySelector("div.pcard_large_sub_txt > span:nth-child(2)")?.Text().Trim()
            };

            if (string.IsNullOrWhiteSpace(info.Title)) {
                info.Title = dom.QuerySelector("div.vplay_info > a.tit")?.Text().Trim();
            }

            if (string.IsNullOrWhiteSpace(info.UploadAt)) {
                info.UploadAt = dom.QuerySelector("div.noti > span.txt")?.Text().Trim();
            }
            return info;
        }

        private async Task<string> GetTitle(string url, Regex re) {
            var stream = await httpClient.GetStreamAsync(url);
            var buf = new byte[4 * 1024];
            await stream.ReadAsync(buf, 0, buf.Length);

            var match = re.Match(Encoding.UTF8.GetString(buf));
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        private struct VLiveInfo {
            public bool VPlus { get; set; }
            public string Title { get; set; }
            public string Views { get; set; }
            public string Duration { get; set; }
            public string UploadAt { get; set; }

            public override string ToString() {
                var sb = new StringBuilder();
                if (VPlus) {
                    sb.Append("[V+] ");
                }

                sb.Append($"{Title}");

                if (VPlus) {
                    if (!string.IsNullOrWhiteSpace(Duration)) {
                        sb.Append($" | {Duration}");
                    }
                }
                else {
                    if (!string.IsNullOrWhiteSpace(Views)) {
                        sb.Append($" | {Views}");
                    }
                }

                sb.Append($" · {UploadAt}");
                return sb.ToString();
            }
        }
    }
}