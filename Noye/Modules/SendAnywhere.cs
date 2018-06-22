namespace Noye.Modules {
    using System;
    using System.Linq;

    public class SendAnywhere : Module {
        private readonly string apiKey;

        public SendAnywhere(INoye noye) : base(noye) {
            apiKey = ApiKeyConfig.Get<SendAnywhereConfig>();
        }

        public override void Register() {
            Noye.Passive(this, @"(sendanywhe\.re|send-anywhere\.com)/.*?(?<id>[^/]*$)", async env => {
                var req = $"?device_key={apiKey}&mode=list&start_pos=0&end_pos=30";

                await WithContext(env, "couldn't get info for link").TryEach("id", async (id, ctx) => {
                    var info = await httpClient.GetAnonymous(
                        $"https://send-anywhere.com/web/key/inquiry/{id}?device_key={apiKey}", new {
                            key = default(string),
                            server = default(string),
                            expires_time = default(long),
                            created_time = default(long)
                        });

                    var list = await httpClient.GetAnonymous(info.server + "/webfile/" + id + req, new {
                        file = new[] {
                            new {
                                downloadable = default(bool),
                                name = default(string),
                                key = default(string),
                                size = default(long),
                                time = default(long)
                            }
                        }
                    });

                    var ts = TimeSpan.FromSeconds(info.expires_time - info.created_time);
                    foreach (var item in list.file.Where(e => e.downloadable)) {
                        var resp = $"[{item.size.AsFileSize()}] {item.name} ({id}, expires in ~{ts.RelativeTime()})";
                        await Noye.Say(env, resp, ctx);
                    }
                });
            });
        }
    }
}