namespace Noye.Modules {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;

    public class Markov : Module {
        private const string address = "http://45.23.161.18:7878";

        private readonly Dictionary<char, char> paired = new Dictionary<char, char> {
            {'(', ')'},
            {'[', ']'},
            {'"', '"'}
        };

        private readonly Regex punctuation = new Regex("([^\\w\\s]|noye)", RegexOptions.Compiled);

        private readonly Random random = new Random(Guid.NewGuid().GetHashCode());
        private readonly Regex url = new Regex("(https?://|:\\d{1,4})", RegexOptions.Compiled);

        public Markov(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Command(this, "brain", async env => {
                var resp = await httpClient.GetAnonymous($"{address}/markov/stats",
                    new {
                        chain_keys = default(ulong),
                        link_sets = default(ulong),
                        entry_points = default(ulong),
                        process_memory = default(ulong),
                        brain_file_size = default(ulong)
                    });

                await Noye.Reply(env,
                    $"keys={resp.chain_keys.WithCommas()} " +
                    $"sets={resp.link_sets.WithCommas()} " +
                    $"entries={resp.entry_points.WithCommas()} " +
                    $"memory={resp.process_memory.WithCommas()} KB " +
                    $"brain={resp.brain_file_size.WithCommas()} KB");
            });

            Noye.Event(this, "PRIVMSG", async msg => {
                if (url.IsMatch(msg.Data) || msg.Data.StartsWith("!")) {
                    return;
                }

                if (random.NextDouble() > 0.9 || msg.Data.IndexOf("noye", StringComparison.OrdinalIgnoreCase) >= 0) {
                    var line = msg.Data;
                    if (line.IndexOf("noye", StringComparison.OrdinalIgnoreCase) >= 0) {
                        line = line.Replace("noye", "", StringComparison.OrdinalIgnoreCase);
                    }

                    line = punctuation.Replace(line, "");
                    line = Regex.Replace(line, "\\s+", " ");
                    line = line.Trim();

                    string data;
                    if (line.Count(e => e == ' ') > 5 || msg.Data.Contains("?")) {
                        do {
                            var resp = await httpClient.PostAsync($"{address}/markov/choose",
                                new StringContent(line));
                            data = await resp.Content.ReadAsStringAsync();
                        }
                        while (line.Length > 10 && !line.Contains(' '));
                    }
                    else {
                        data = await httpClient.GetStringAsync($"{address}/markov/next");
                    }

                    data = FixPairing(data);
                    data = data.Replace("\"\"", ""); // remove ""
                    data = data.Replace(">.", ".");  // remove ,.
                    data = data.Replace(",.", ".");  // remove >.
                    data = data.Replace("*.", "."); // remove >.
                    
                    await Noye.Raw($"PRIVMSG {msg.Parameters[0]} :{data}\r\n");
                }

                await httpClient.PostAsync($"{address}/markov/train", new StringContent(msg.Data));
            });
        }

        private string FixPairing(string input) {
            var seen = new Stack<char>();
            foreach (var p in paired.Keys) {
                var count = input.Count(c => c == p);
                if (count == 0) {
                    continue;
                }

                var other = input.Count(c => c == paired[p]);
                if (count > 1 && count > other) {
                    for (var i = 0; i < count - other; i++) {
                        seen.Push(p);
                    }
                }
                else if (count == 1) {
                    seen.Push(p);
                }
            }

            foreach (var p in seen) {
                input = input.Insert(input.Length - 1, paired[p].ToString());
            }

            return input;
        }
    }
}