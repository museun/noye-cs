namespace Noye {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;

    public static class NoyeExtensions {
        public static IEnumerable<string> SplitAt(this string input, int max) {
            var index = 0;
            while (index < input.Length) {
                if (index + max < input.Length) {
                    yield return input.Substring(index, max);
                }
                else {
                    yield return input.Substring(index);
                }

                index += max;
            }
        }

        public static string Slice(this string data, int begin, int end) {
            if (end < 0) {
                end = data.Length;
            }

            return data.Substring(begin, end - begin);
        }

        public static async Task TryEach(this Context ctx, string item, Func<string, Context, Task> fn) {
            var items = ctx.Envelope.Matches.Get(item);
            if (items.Count == 0) {
                Log.Warning("({class}) no items found for '{item}'", ctx.Name, item);
                return;
            }

            var tasks = new List<Task>();
            foreach (var el in items) {
                var local = ctx.Clone() as Context;
                Debug.Assert(local != null, nameof(local) + " != null");
                local.Data = el;

                tasks.Add(fn(el, local).ContinueWith(t => {
                    if (t.Exception == null) {
                        return;
                    }

                    Exception ex = t.Exception;
                    while (ex is AggregateException && ex.InnerException != null) {
                        ex = ex.InnerException;
                    }

                    Log.Warning("({class}) [{sender} @ {target}] caught an exception for {id}: {ex}", local.Name,
                        local.Sender, local.Target, el, ex.Message);
                }, TaskContinuationOptions.OnlyOnFaulted));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    public static class ThreadLocalRandom {
        private static readonly Random globalRandom = new Random();
        private static readonly object globalLock = new object();
        private static readonly ThreadLocal<Random> threadRandom = new ThreadLocal<Random>(NewRandom);
        public static Random Instance => threadRandom.Value;

        public static Random NewRandom() {
            lock (globalLock) return new Random(globalRandom.Next());
        }

        public static int Next() => Instance.Next();
        public static int Next(int max) => Instance.Next(max);
        public static int Next(int min, int max) => Instance.Next(min, max);
        public static double NextDouble() => Instance.NextDouble();
        public static void NextBytes(byte[] buffer) => Instance.NextBytes(buffer);
    }

    public static class Utilities {
        public static string GetIpAddress() {
            using (var http = new System.Net.Http.HttpClient()) {
                var res = http.GetStringAsync("http://ifconfig.co/ip").Result.Trim();
                return !IPAddress.TryParse(res, out _) ? null : res;
            }
        }
    }

    public static class HttpExtensions {
        private static readonly System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();

        public static async Task<HttpContentHeaders> GetHeaders(string link) {
            var req = new HttpRequestMessage(HttpMethod.Get, link);
            try {
                var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                var headers = resp.Content.Headers;
                return headers;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }

            return null;
        }
    }
}