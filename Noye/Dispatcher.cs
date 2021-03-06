﻿namespace Noye {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Serilog;

    public class Dispatcher {
        private readonly List<Active> actives = new List<Active>();
        private readonly List<Event> events = new List<Event>();
        private readonly List<Passive> passives = new List<Passive>();

        public void Command(Module module, string trigger, Func<Envelope, Task> func) {
            var active = new Active(module.GetType().Name, trigger, func);
            Log.Verbose("({name}) adding active: {trigger}", active.Name, active.Trigger);
            actives.Add(active);
        }

        public void Passive(Module module, string pattern, Func<Envelope, Task> func) {
            var passive = new Passive(module.GetType().Name, pattern, func);
            Log.Verbose("({name}) adding passive: {pattern}", passive.Name, passive.Pattern);
            passives.Add(passive);
        }

        public void Event(Module module, string command, Func<Message, Task> func) {
            var ev = new Event(module.GetType().Name, command, func);
            Log.Verbose("({name}) adding event: {command}", ev.Name, ev.Command);
            events.Add(ev);
        }

        public async void Dispatch(Message msg) {
            var tasks = new List<Task>();

            // try user-triggered commands first
            if (msg.Command == "PRIVMSG") {
                // try all of the !commands first.
                tasks.AddRange(CollectActives(msg));

                // then the regex commands
                tasks.AddRange(CollectPassives(msg));
                if (tasks.Count > 0) {
                    // should maybe do a shadow copy of this, or find a way to do a cheap clone/cache.
                    // or maybe extract out the sender/target parse
                    var env = new Envelope(msg);
                    Log.Debug("PRIVMSG: [{nick} @ {channel}]: {message}", env.Sender, env.Target, msg.Data.Trim());
                }
            }

            // finally the catch all matched events
            tasks.AddRange(CollectEvents(msg));

            tasks.ForEach(t => t.Start());
            await Task.WhenAll(tasks);
        }

        private IEnumerable<Task> CollectActives(Message msg) {
            var tasks = new List<Task>();
            foreach (var active in actives.Where(a => msg.Data.StartsWith(a.Trigger))) {
                var param = msg.Data.Substring(active.Trigger.Length).Trim();
                var env = new Envelope(msg, string.IsNullOrWhiteSpace(param) ? null : param);
                tasks.Add(new Task(async () => await active.Action(env)));
            }

            return tasks;
        }

        private IEnumerable<Task> CollectPassives(Message msg) {
            var tasks = new List<Task>();
            foreach (var passive in passives.Where(p => p.Pattern.IsMatch(msg.Data))) {
                var env = new Envelope(msg, matches: new Matches(passive.Pattern, msg.Data));
                tasks.Add(new Task(async () => await passive.Action(env)));
            }

            return tasks;
        }

        private IEnumerable<Task> CollectEvents(Message msg) {
            Task filter(Event ev) {
                void error(Task t) {
                    t?.Exception?.Flatten().Handle(ex => {
                        Log.Warning(ex, "({name}) unhandled exception for {@msg}", ev.Name, msg);
                        return true;
                    });
                }

                return new Task(async () => await ev.Action(msg).ContinueWith(error));
            }

            return events.Where(ev => ev.Command == msg.Command).Select(filter);
        }
    }
}