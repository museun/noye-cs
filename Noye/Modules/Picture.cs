﻿namespace Noye.Modules {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Web;
    using Nancy;

    public class PictureModule : Module {
        private readonly IReadOnlyDictionary<string, PicturesConfig.Item> pictures;
        private readonly Random random = new Random(DateTime.Now.Millisecond);

        public PictureModule(INoye noye) : base(noye) {
            var conf = ModuleConfig.Get<PicturesConfig>();
            pictures = conf.Directorties;

            var ps = Noye.Resolve<PictureServe>();
            foreach (var p in pictures) {
                ps.mapping.GetOrAdd(p.Key, new InnerServe());
            }
        }

        public override void Register() {
            var host = Noye.GetHostAddress();

            Noye.Command("pictures", async env => {
                var list = pictures.Select(p => $"!{p.Key}");
                await Noye.Reply(env, string.Join(" ", list));
            });

            foreach (var kv in pictures) {
                Noye.Command(kv.Value.Command, async env => {
                    if (env.Param == "list") {
                        return;
                    }

                    var file = SelectFileFor(kv.Value);
                    var ps = Noye.Resolve<PictureServe>();
                    if (ps.mapping.TryGetValue(kv.Key, out var serve)) {
                        var id = serve.Store(new PictureServe.Item(file));
                        await Noye.Reply(env, $"http://{host}/{kv.Key}/{id}");
                    }
                });

                Noye.Command($"{kv.Value.Command} list", async env => {
                    if (!await Noye.CheckAuth(env)) {
                        return;
                    }

                    var ps = Noye.Resolve<PictureServe>();
                    if (ps.mapping.TryGetValue(kv.Key, out var serve)) {
                        var list = string.Join(Environment.NewLine, GetPreviousFor(serve as InnerServe));
                        var id = Noye.Resolve<MemoryServe>()
                            .Store(new MemoryServe.Item("list", MemoryServe.PlainText, list));
                        await Noye.Raw($"PRIVMSG {env.Sender} :http://{host}/s/{id}");
                    }
                });

                Noye.Event("PRIVMSG", async message => {
                    var chan = message.Parameters[0];
                    if (kv.Value.BannedChannels.Any(e => e == chan)) {
                        return;
                    }

                    if (random.Next(0, kv.Value.Chance) == 0) {
                        var file = SelectFileFor(kv.Value);
                        var ps = Noye.Resolve<PictureServe>();
                        if (ps.mapping.TryGetValue(kv.Key, out var serve)) {
                            var id = serve.Store(new PictureServe.Item(file));
                            await Noye.Raw($"PRIVMSG {chan} :http://{host}/{kv.Key}/{id}");
                        }
                    }
                });
            }
        }

        private string SelectFileFor(PicturesConfig.Item pic) {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                ".jpg",
                ".jpeg",
                ".gif",
                ".png"
            };

            var list = Directory.EnumerateFiles(pic.Directory, "*.*", SearchOption.AllDirectories)
                .Where(e => exts.Contains(Path.GetExtension(e))).ToList();

            return list[random.Next(list.Count)];
        }

        private IEnumerable<string> GetPreviousFor(InnerServe inner) {
            foreach (var prev in inner.List()) {
                var time = ((DateTimeOffset) prev.Value.DeleteAt).ToUnixTimeSeconds();
                yield return $"{prev.Key}\t{time}\t{prev.Value.Filepath}";
            }
        }

        private class InnerServe : AbstractServe<PictureServe.Item> { }
    }

    public class PictureServe {
        public readonly ConcurrentDictionary<string, AbstractServe<Item>> mapping
            = new ConcurrentDictionary<string, AbstractServe<Item>>();

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

    public class PictureServeModule : NancyModule {
        public PictureServeModule(IReadOnlyList<PictureServe> serves) {
            Get["{route}/{id}"] = parameters => {
                var serve = serves.Select(e => {
                    if (e.mapping.TryGetValue(parameters.route, out AbstractServe<PictureServe.Item> s)) {
                        return s;
                    }

                    return null;
                }).FirstOrDefault();

                if (serve == null) {
                    return new Response {StatusCode = HttpStatusCode.BadRequest};
                }

                if (serve.Retrieve(parameters.id) is PictureServe.Item item) {
                    var fi = new FileInfo(item.Filepath);
                    var resp = new Response {
                        ContentType = MimeMapping.GetMimeMapping(item.Filepath),
                        Headers = {
                            ["ETag"] = fi.LastWriteTimeUtc.Ticks.ToString("x"),
                            ["Last-Modified"] = fi.LastWriteTimeUtc.ToString("R"),
                            ["Content-Disposition"] =
                                $"inline; filename*=UTF-8''{HttpUtility.UrlPathEncode(Path.GetFileName(item.Filepath))}",
                            ["Content-Length"] = $"{fi.Length}"
                        },
                        Contents = stream => {
                            using (var file = File.OpenRead(item.Filepath)) {
                                file.CopyTo(stream);
                            }
                        }
                    };
                    return resp;
                }

                var error = Response.AsText($"id not found {parameters.id}");
                error.StatusCode = HttpStatusCode.BadRequest;
                return error;
            };
        }
    }
}