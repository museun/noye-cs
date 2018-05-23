namespace Noye {
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public class Active {
        public Active(string trigger, Func<Envelope, Task> action) {
            Action = action;
            Trigger = trigger;
        }

        public Func<Envelope, Task> Action { get; }
        public string Trigger { get; }
    }

    public class Passive {
        public Passive(string pattern, Func<Envelope, Task> action) {
            Action = action;
            Pattern = new Regex(pattern,
                RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
        }

        public Regex Pattern { get; }
        public Func<Envelope, Task> Action { get; }
    }

    public class Event {
        public Event(string command, Func<Message, Task> action) {
            Command = command;
            Action = action;
        }

        public string Command { get; }
        public Func<Message, Task> Action { get; }
    }
}