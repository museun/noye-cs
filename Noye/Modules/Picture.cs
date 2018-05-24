namespace Noye.Modules {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web;
    using Nancy;

    public class PictureModule : Module {
        private readonly Dictionary<string, IReadOnlyList<string>> cache =
            new Dictionary<string, IReadOnlyList<string>>();

        private readonly Dictionary<string, ItemContext> pictures = 
            new Dictionary<string, ItemContext>();

        private readonly Random random = new Random(DateTime.Now.Millisecond);
        private readonly IReadOnlyList<FileSystemWatcher> watchers;

        public PictureModule(INoye noye) : base(noye) {
            var list = new List<FileSystemWatcher>();
            var ps = Noye.Resolve<PictureServe>();
            var conf = ModuleConfig.Get<PicturesConfig>();

            foreach (var p in conf.Directorties) {
                var ctx = new ItemContext {Item = p.Value};
                ctx.Item.Directory = Path.GetFullPath(ctx.Item.Directory);

                var watcher = new FileSystemWatcher(ctx.Item.Directory);
                watcher.Changed += (s, e) => { ctx.With(self => self.Dirty = true); };
                watcher.Created += (s, e) => { ctx.With(self => self.Dirty = true); };
                watcher.Deleted += (s, e) => { ctx.With(self => self.Dirty = true); };
                watcher.Renamed += (s, e) => { ctx.With(self => self.Dirty = true); };
                watcher.EnableRaisingEvents = true;

                list.Add(watcher);

                pictures[p.Key] = ctx;
                ps.mapping.GetOrAdd(p.Key, new InnerServe());
            }

            watchers = list;
        }

        public override void Register() {
            var host = Noye.GetHostAddress();

            Noye.Command("pictures", async env => {
                var list = pictures.Select(p => $"!{p.Key}");
                await Noye.Reply(env, string.Join(" ", list));
            });

            foreach (var kv in pictures) {
                Noye.Command(kv.Value.Item.Command, async env => {
                    if (env.Param == "list" || env.Param == "chance") {
                        return;
                    }

                    var file = SelectFileFor(kv.Value);
                    var ps = Noye.Resolve<PictureServe>();
                    if (ps.mapping.TryGetValue(kv.Key, out var serve)) {
                        var id = serve.Store(new PictureServe.Item(file));
                        await Noye.Reply(env, $"http://{host}/{kv.Key}/{id}");
                    }
                });

                Noye.Event("PRIVMSG", async message => {
                    var chan = message.Parameters[0];
                    if (kv.Value.Item.BannedChannels.Any(e => e == chan)) {
                        return;
                    }

                    if (random.Next(0, kv.Value.Item.Chance) == 0) {
                        var file = SelectFileFor(kv.Value);
                        var ps = Noye.Resolve<PictureServe>();
                        if (ps.mapping.TryGetValue(kv.Key, out var serve)) {
                            var id = serve.Store(new PictureServe.Item(file));
                            await Noye.Raw($"PRIVMSG {chan} :http://{host}/{kv.Key}/{id}");
                        }
                    }
                });

                Noye.Command($"{kv.Value.Item.Command} chance", async env => {
                    if (!await Noye.CheckAuth(env)) {
                        return;
                    }

                    if (int.TryParse(env.Param, out var ch)) {
                        var old = kv.Value.Item.Chance;
                        kv.Value.With(self => self.Item.Chance = ch);

                        await Noye.Reply(env, $"changed the chance to 1/{ch} from 1/{old}");
                        return;
                    }

                    await Noye.Reply(env, $"current chance: 1/{kv.Value.Item.Chance}");
                });

                Noye.Command($"{kv.Value.Item.Command} list", async env => {
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
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            foreach (var watcher in watchers) {
                watcher.Dispose();
            }
        }

        private IReadOnlyList<string> GetFiles(string directory) {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                ".jpg",
                ".jpeg",
                ".gif",
                ".png"
            };

            return Directory
                .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(e => exts.Contains(Path.GetExtension(e)))
                .ToList();
        }

        private string SelectFileFor(ItemContext pic) {
            IReadOnlyList<string> list;
            if (pic.Dirty || !cache.ContainsKey(pic.Item.Directory)) {
                list = GetFiles(pic.Item.Directory);
                if (cache.ContainsKey(pic.Item.Directory)) {
                    cache[pic.Item.Directory] = list;
                }
                else {
                    cache.Add(pic.Item.Directory, list);
                }

                pic.With(self => self.Dirty = false);
            }
            else {
                list = cache[pic.Item.Directory];
            }

            return list[random.Next(list.Count)];
        }

        private static IEnumerable<string> GetPreviousFor(InnerServe inner) {
            foreach (var prev in inner.List().OrderBy(e => e.Value.DeleteAt)) {
                var time = ((DateTimeOffset) prev.Value.DeleteAt).ToUnixTimeSeconds();
                yield return $"{prev.Key}\t{time}\t{prev.Value.Filepath}";
            }
        }

        private class ItemContext {
            private readonly object locker = new object();
            public PicturesConfig.Item Item { get; set; }
            public bool Dirty { get; internal set; }

            public void With(Action<ItemContext> fn) {
                lock (locker) {
                    fn(this);
                }
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