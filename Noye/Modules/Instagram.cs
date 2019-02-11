namespace Noye.Modules {
    using System;
    using System.Text.RegularExpressions;


//            <title>
//モーニング娘。&#39;19 on Instagram: “モーニング娘。&#39;19 LOVEオーディション ８人目のコメントは！ 13期メンバー 加賀楓です🍁  オーディションを受ける前はダンスをやったことがなかったから、Dance shotを見てよく真似してました、、。 モーニング娘。の曲はダンスも歌も楽しいんですよ！！！…”
//</title>


    public class Instagram : Module {
        private readonly Regex displayRegex = new Regex(@"""full_name"":\s?""(?<name>.*?)""",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly Regex nameRegex = new Regex("<meta content=\".*?\\s\\((?<name>@.*?)\\)\\s",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public Instagram(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(this, @"(?<url>(?:www|https?)?instagram\.com\/p\/[^\s]+)", async env =>
            {
                await WithContext(env, "cannot find data").TryEach("url", async (url, ctx) =>
                {
                    var body = await httpClient.GetStringAsync("https://" + url);
                    var parser = new AngleSharp.Parser.Html.HtmlParser();
                    var document = parser.Parse(body);
                    var html_title = document.Title.Split(new string[] { "on Instagram:" }, StringSplitOptions.None);

                    string title = string.Empty;
                    if (html_title.Length == 0)
                    {
                        title = displayRegex.Match(body).Groups["name"].Value;
                    }
                    else
                    {
                        title = html_title[0].Trim();
                    }

                    var display = FixIt(title);
                    var name = FixIt(nameRegex.Match(body).Groups["name"].Value);

                    if (string.IsNullOrWhiteSpace(display)) {
                        await Noye.Say(env, name, ctx);
                    }
                    else {
                        await Noye.Say(env, $"{display} ({name})", ctx);
                    }
                });
            });
        }

        private static string FixIt(string data) {
            return Regex.Replace(Regex.Replace(data, @"\\u([\dA-Fa-f]{4})",
                v => ((char) Convert.ToInt32(v.Groups[1].Value, 16)).ToString()), " {2,}", " ");
        }
    }
}