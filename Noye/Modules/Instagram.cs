namespace Noye.Modules {
    using System;
    using System.Text.RegularExpressions;

    public class Instagram : Module {
        private readonly Regex displayRegex = new Regex(
            @"""full_name"":\s?""(?<name>.*?)""",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly Regex nameRegex = new Regex(
            "<meta content=\".*?\\s\\((?<name>@.*?)\\)\\s",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public Instagram(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(@"(?<url>(?:www|https?)?instagram\.com\/p\/[^\s]+)", async env => {
                foreach (var url in env.Matches.Get("url")) {
                    var body = await httpClient.GetStringAsync("https://" + url);
                    var title = FixIt(displayRegex.Match(body).Groups["name"].Value);
                    var name = FixIt(nameRegex.Match(body).Groups["name"].Value);

                    await Noye.Say(env, !string.IsNullOrWhiteSpace(title)
                        ? $"{title} ({name})"
                        : $"{name}");
                }
            });
        }

        private static string FixIt(string data) {
            return Regex.Replace(Regex.Replace(data, @"\\u([\dA-Fa-f]{4})",
                v => ((char) Convert.ToInt32(v.Groups[1].Value, 16)).ToString()), " {2,}", " ");
        }
    }
}