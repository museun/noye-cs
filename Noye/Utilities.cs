namespace Noye {
    using System.Net;

    public static class Utilities {
        public static string GetIpAddress() {
            using (var http = new System.Net.Http.HttpClient()) {
                var res = http.GetStringAsync("http://ifconfig.co/ip").Result.Trim();
                return !IPAddress.TryParse(res, out _) ? null : res;
            }
        }
    }
}