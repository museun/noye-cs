namespace Noye {
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Serilog;

    public class Active {
        public Active(string name, string trigger, Func<Envelope, Task> action) {
            Name = name;
            Trigger = "!" + trigger; // TODO make the prefix configurable

            Action = async env => {
                void error(Task t) {
                    t?.Exception?.Flatten().Handle(ex => {
                        Log.Warning(ex, "({name}) unhandled exception for {trigger}", Name, Trigger);
                        return true;
                    });
                }

                Log.Debug("({name}) running action: {trigger}", Name, Trigger);
                await action(env).ContinueWith(error);
            };
        }

        public Func<Envelope, Task> Action { get; }
        public string Trigger { get; }
        public string Name { get; }
    }

    public class Passive {
        public Passive(string name, string pattern, Func<Envelope, Task> action) {
            Name = name;
            Pattern = new Regex(pattern,
                RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

            Action = async env => {
                void error(Task t) {
                    t?.Exception?.Flatten().Handle(ex => {
                        Log.Warning(ex, "({name}) unhandled exception for {pattern}", Name, Pattern);
                        return true;
                    });
                }

                Log.Debug("({name}) running passive: {pattern}", Name, Pattern);
                await action(env).ContinueWith(error);
            };
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