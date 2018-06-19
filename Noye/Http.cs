namespace Noye {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Nancy;
    using Nancy.Hosting.Self;
    using Newtonsoft.Json;
    using Serilog;

    public interface IServe<T> {
        string GenerateId();
        string Store(T item);
        T Retrieve(string id);
        T Remove(string id);
    }

    public interface IItem {
        DateTime DeleteAt { get; }
    }

    public abstract class AbstractServe<T> : IDisposable, IServe<T> where T : class, IItem {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly TimeSpan delay = TimeSpan.FromSeconds(1 * 30); // every 30 seconds do a sweep
        private readonly Random rand = new Random(DateTime.Now.Millisecond);

        protected readonly ConcurrentDictionary<string, T> store = new ConcurrentDictionary<string, T>();

        protected AbstractServe() {
            Task.Run(async () => {
                var token = cts.Token;
                while (!token.IsCancellationRequested) {
                    await Task.Delay(delay, token);
                    if (token.IsCancellationRequested) {
                        return;
                    }

                    var now = DateTime.Now;
                    foreach (var kv in store.Where(kv => kv.Value.DeleteAt < now)) {
                        if (store.TryRemove(kv.Key, out var item)) {
                            Clean(item);
                        }
                    }
                }
            }, cts.Token);
        }

        protected int Length { get; set; } = 4;

        public virtual void Dispose() {
            store.Clear();
            cts?.Dispose();
        }

        public virtual string GenerateId() {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxy";
            return string.Join("", Enumerable.Repeat(chars, Length).Select(s => s[rand.Next(s.Length)]));
        }

        public string Store(T item) {
            var id = GenerateId();
            store.GetOrAdd(id, item);
            return id;
        }

        public T Retrieve(string id) => store.ContainsKey(id) ? store[id] : null;

        public T Remove(string id) {
            store.TryRemove(id, out var item);
            return item;
        }

        public IReadOnlyDictionary<string, T> List() => store;

        public virtual void Clean(T item) { }
    }

    public class MemoryServe : AbstractServe<MemoryServe.Item> {
        public const string PlainText = "text/plain;charset=utf-8";

        public class Item : IItem {
            public Item(string name, string type, string data)
                : this(name, type, data, TimeSpan.FromMinutes(15)) { }

            public Item(string name, string type, string data, TimeSpan delete)
                : this(name, type, Encoding.UTF8.GetBytes(data), delete) { }

            public Item(string name, string type, byte[] data, TimeSpan delete) {
                Name = name;
                ContentType = type;
                DeleteAt = DateTime.Now.Add(delete);
                Data = data;
            }

            public string Name { get; }
            public byte[] Data { get; }
            public string ContentType { get; }

            public DateTime DeleteAt { get; }
        }
    }

    public class HttpServer : IDisposable {
        private readonly NancyHost host;

        public HttpServer(IContainer container) {
            var conf = Configuration.Load().Http;
            host = new NancyHost(
                new Uri($"http://{conf.Bind}:{conf.Port}"), // bind host
                new NancyBootstrapper(container),           // ioc container
                new HostConfiguration {
                    UrlReservations = new UrlReservations {
                        CreateAutomatically = true
                    }
                });

            Log.Information($"starting http server on {conf.Bind}:{conf.Port}");
            host.Start();
        }

        public void Dispose() => host?.Dispose();
    }

    public class MemoryServeRedirectModule : NancyModule {
        public MemoryServeRedirectModule(MemoryServe ms) : base("/t") {
            Get["/{id}"] = parameters => {
                if (!(ms.Retrieve(parameters["id"]) is MemoryServe.Item item)) {
                    var error = Response.AsText($"id not found {parameters["id"]}");
                    error.StatusCode = HttpStatusCode.BadRequest;
                    return error;
                }

                // for 302, instead of RedirectResponse
                var resp = new Response {StatusCode = HttpStatusCode.Found};
                resp.Headers.Add("Location", Encoding.UTF8.GetString(item.Data));
                return resp;
            };
        }
    }

    public class MemoryServeDownloadModule : NancyModule {
        public MemoryServeDownloadModule(MemoryServe ms) : base("/x") {
            Get["/{id}"] = parameters => {
                if (!(ms.Retrieve(parameters["id"]) is MemoryServe.Item item)) {
                    var error = Response.AsText($"id not found {parameters["id"]}");
                    error.StatusCode = HttpStatusCode.BadRequest;
                    return error;
                }

                var resp = new Response {
                    Contents = stream => stream.Write(item.Data, 0, item.Data.Length),
                    ContentType = item.ContentType
                };

                return resp.AsAttachment(item.Name);
            };
        }
    }

    public class MemoryServeModule : NancyModule {
        public MemoryServeModule(MemoryServe ms) : base("/s") {
            Get["/{id}"] = parameters => {
                if (!(ms.Retrieve(parameters["id"]) is MemoryServe.Item item)) {
                    var error = Response.AsText($"id not found {parameters["id"]}");
                    error.StatusCode = HttpStatusCode.BadRequest;
                    return error;
                }

                var resp = new Response {
                    Contents = stream => stream.Write(item.Data, 0, item.Data.Length),
                    ContentType = item.ContentType
                };

                return resp;
            };
        }
    }

    public static class NancyExtensions {
        public static void CheckForIfNonMatch(this NancyContext ctx) {
            var req = ctx.Request;
            var resp = ctx.Response;

            if (!resp.Headers.TryGetValue("ETag", out var etag)) return;
            if (req.Headers.IfNoneMatch.Contains(etag)) {
                ctx.Response = HttpStatusCode.NotModified;
            }
        }

        public static void CheckForIfModifiedSince(this NancyContext ctx) {
            var req = ctx.Request;
            var resp = ctx.Response;

            if (!resp.Headers.TryGetValue("Last-Modified", out var modified)) {
                return;
            }

            if (!req.Headers.IfModifiedSince.HasValue || !DateTime.TryParseExact(modified, "R",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) {
                return;
            }

            if (time <= req.Headers.IfModifiedSince.Value) {
                ctx.Response = HttpStatusCode.NotModified;
            }
        }
    }

    public class HttpClient : System.Net.Http.HttpClient {
        public HttpClient() {}

        public HttpClient(HttpMessageHandler handler) : base(handler) { }

        public async Task<T> GetAnonymous<T>(HttpRequestMessage req, T type) {
            var resp = await SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeAnonymousType(json, type);
        }

        public async Task<T> Get<T>(HttpRequestMessage req) {
            var resp = await SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }

        public async Task<T> GetAnonymous<T>(string url, T type) {
            var json = await GetStringAsync(url);
            return JsonConvert.DeserializeAnonymousType(json, type);
        }

        public async Task<T> Get<T>(string url, T type) {
            var json = await GetStringAsync(url);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}