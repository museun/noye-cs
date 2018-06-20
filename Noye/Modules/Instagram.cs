﻿namespace Noye.Modules {
    using System;
    using System.Text.RegularExpressions;

    public class Instagram : Module {
        private readonly Regex displayRegex = new Regex(@"""full_name"":\s?""(?<name>.*?)""",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly Regex nameRegex = new Regex("<meta content=\".*?\\s\\((?<name>@.*?)\\)\\s",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public Instagram(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(@"(?<url>(?:www|https?)?instagram\.com\/p\/[^\s]+)", async env => {
                await env.TryEach("url", WithContext(env, "cannot find data"), async (url, ctx) => {
                    var body = await httpClient.GetStringAsync("https://" + url);
                    var display = FixIt(displayRegex.Match(body).Groups["name"].Value);
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