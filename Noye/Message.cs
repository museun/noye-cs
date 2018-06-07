namespace Noye {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Message {
        public Message() { }

        public Message(string data) {
            if (data[0] == ':') {
                var end = data.IndexOf(" ", StringComparison.Ordinal);
                if (end >= -1) {
                    Prefix = data.Slice(1, end);
                    data = data.Slice(end + 1, data.Length);
                }
            }

            var parts = data.Split(new[] {" :"}, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1) {
                Data = parts[1].Trim();
            }

            var args = parts[0].Split(' ');
            Command = args[0].ToUpper();
            if (args.Length > 1) {
                Parameters = args.Skip(1).ToList();
            }

            if (string.IsNullOrWhiteSpace(Data)) {
                Data = " "; // if no data was parsed, give it a 0x20 just for sanity reasons
            }
        }

        public string Prefix { get; set; }
        public string Command { get; set; }
        public List<string> Parameters { get; set; } = new List<string>();
        public string Data { get; set; }
    }
}