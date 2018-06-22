namespace Noye {
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class Active {
        public Active(string name, string trigger, Func<Envelope, Task> action) {
            Name = name;
            Action = action;
            Trigger = "!" + trigger; // TODO make the prefix configurable
        }

        public Func<Envelope, Task> Action { get; }
        public string Trigger { get; }
        public string Name { get; }
    }

    public class Passive {
        public Passive(string name, string pattern, Func<Envelope, Task> action) {
            Name = name;
            Action = action;
            Pattern = new Regex(pattern,
                RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
        }

        public Regex Pattern { get; }
        public Func<Envelope, Task> Action { get; }
        public string Name { get; }
    }

    public class Event {
        public Event(string name, string command, Func<Message, Task> action) {
            Name = name;
            Command = command;
            Action = action;
        }

        public string Command { get; }
        public Func<Message, Task> Action { get; }
        public string Name { get; }
    }
}