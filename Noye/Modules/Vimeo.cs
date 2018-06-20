namespace Noye.Modules {
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public class Vimeo : Module {
        public Vimeo(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(@"vimeo\.com\/(?<vid>\d+)", async env => {
                await env.TryEach("vid", WithContext(env, "cannot find video"), async (vid, ctx) => {
                    var req = new HttpRequestMessage(HttpMethod.Get, $"https://player.vimeo.com/video/{vid}/config");
                    req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));

                    var json = await httpClient.GetAnonymous(req, new {
                        video = new {
                            duration = default(long),
                            id = default(long),
                            title = default(string),
                            owner = new {
                                name = default(string)
                            }
                        }
                    });
                    var video = json?.video;
                    if (video == null) {
                        return;
                    }

                    var duration = TimeSpan.FromSeconds(video.duration).AsShortTime();
                    var resp = $"{video.title} | {duration} · {video.owner.name} | https://vimeo.com/{video.id}";
                    await Noye.Say(env, resp);
                });
            });
        }
    }
}