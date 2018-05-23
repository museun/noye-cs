namespace Noye.Modules {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Nancy;

    public class Sayumi : Module {
        private readonly List<string> banned;
        private readonly int chance;
        private readonly string directory;

        private readonly Random random = new Random(DateTime.Now.Millisecond);

        public Sayumi(INoye noye) : base(noye) {
            var conf = ModuleConfig.Get<SayumiConfig>();
            directory = conf.Directory;
            chance = conf.Chance;
            banned = conf.BannedChannels.ToList();
        }

        private IEnumerable<string> GetPrevious() {
            var ss = Noye.Resolve<SayumiServe>();
            foreach (var prev in ss.List()) {
                var time = ((DateTimeOffset) prev.Value.DeleteAt).ToUnixTimeSeconds();
                yield return $"{prev.Key}\t{time}\t{prev.Value.Filepath}";
            }
        }

        public override void Register() {
            var host = Noye.GetHostAddress();

            Noye.Command("sayumi", async env => {
                if (env.Param == "list") {
                    return;
                }

                var file = SelectFile();
                var id = Noye.Resolve<SayumiServe>().Store(new SayumiServe.Item(file));
                await Noye.Reply(env, $"http://{host}/sayu/{id}");
            });

            Noye.Command("sayumi list", async env => {
                if (!await Noye.CheckAuth(env)) {
                    return;
                }

                var list = string.Join(Environment.NewLine, GetPrevious());
                var id = Noye.Resolve<MemoryServe>().Store(new MemoryServe.Item("list", MemoryServe.PlainText, list));
                await Noye.Raw($"PRIVMSG {env.Sender} :http://{host}/s/{id}");
            });

            Noye.Event("PRIVMSG", async message => {
                var chan = message.Parameters[0];
                if (banned.Any(e => e == chan)) {
                    return;
                }

                if (random.Next(0, chance) == 0) {
                    var file = SelectFile();
                    var id = Noye.Resolve<SayumiServe>().Store(new SayumiServe.Item(file));
                    await Noye.Raw($"PRIVMSG {chan} :http://{host}/sayu/{id}");
                }
            });
        }

        private string SelectFile() {
            var list = Directory.EnumerateFiles(directory, "*.jpg", SearchOption.AllDirectories).ToList();
            return list[random.Next(list.Count)];
        }
    }

    public class SayumiServe : AbstractServe<SayumiServe.Item> {
        public class Item : IItem {
            public Item(string filepath, TimeSpan delete) {
                Filepath = filepath;
                DeleteAt = DateTime.Now.Add(delete);
            }

            public Item(string filepath) :
                this(filepath, TimeSpan.FromMinutes(60)) { }

            public string Filepath { get; }
            public DateTime DeleteAt { get; }
        }
    }

    public class SayumiModule : NancyModule {
        public SayumiModule(SayumiServe ms) : base("/sayu") {
            Get["/{id}"] = parameters => {
                if (ms.Retrieve(parameters["id"]) is SayumiServe.Item item) {
                    var fi = new FileInfo(item.Filepath);
                    var resp = new Response {
                        ContentType = "image/jpg",
                        Headers = {
                            ["ETag"] = fi.LastWriteTimeUtc.Ticks.ToString("x"),
                            ["Last-Modified"] = fi.LastWriteTimeUtc.ToString("R"),
                            ["Content-Disposition"] = $"inline; filename=\"{Path.GetFileName(item.Filepath)}\""
                        },
                        Contents = stream => {
                            using (var file = File.OpenRead(item.Filepath)) file.CopyTo(stream);
                        }
                    };
                    return resp;
                }

                var error = Response.AsText($"id not found {parameters["id"]}");
                error.StatusCode = HttpStatusCode.BadRequest;
                return error;
            };
        }
    }
}