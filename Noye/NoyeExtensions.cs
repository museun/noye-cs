namespace Noye {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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

        public static string Replace(this string source, string old, string replace, StringComparison comp) {
            var index = source.IndexOf(old, comp);
            if (index >= 0) {
                source = source.Remove(index, old.Length);
                source = source.Insert(index, replace);
                return source;
            }
            return source;
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
}