namespace Noye {
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    public class Matches {
        private readonly Dictionary<string, List<string>> map = new Dictionary<string, List<string>>();

        public Matches(Regex re, string input) {
            var names = re.GetGroupNames();
            if (!names.Any()) return;

            var matches = re.Matches(input);
            foreach (var match in matches.Cast<Match>().Where(m => m.Success)) {
                foreach (var name in names.Skip(1)) {
                    var group = match.Groups[name];
                    if (!group.Success) continue;
                    if (!map.ContainsKey(name)) map.Add(name, new List<string>());
                    map[name].Add(group.Value);
                }
            }
        }

        public bool Has(string key) => map.ContainsKey(key);
        public List<string> Get(string key) => map.ContainsKey(key) ? map[key] : new List<string>();
    }
}