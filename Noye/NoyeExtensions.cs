namespace Noye {
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;

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