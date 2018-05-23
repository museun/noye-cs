namespace Noye.Modules {
    using System.Collections.Generic;
    using System.Linq;

    public class LinkSize : Module {
        public LinkSize(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(@"(?<link>(?:www|https?)[^\s]+)", async env => {
                var sizes = new List<KeyValuePair<int, string>>();
                foreach (var link in env.Matches.Get("link").Select((m, i) => new {Index = i + 1, Match = m})) {
                    var headers = await HttpExtensions.GetHeaders(link.Match);
                    var length = headers?.ContentLength;
                    if (length > 10 * 1024 * 1024) {
                        sizes.Add(new KeyValuePair<int, string>(link.Index, length.Value.AsFileSize()));
                    }
                }

                if (sizes.Count > 1) {
                    var result = string.Join(", ", sizes.Select(pair => $"#{pair.Key}: {pair.Value}"));
                    await Noye.Say(env, $"some of those are kind of big: {result}");
                }

                if (sizes.Count == 1) {
                    await Noye.Say(env, $"that file is kind of big: {sizes[0].Value}");
                }
            });
        }
    }
}