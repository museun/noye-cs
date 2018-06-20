namespace Noye {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Serilog;

    public class Dispatcher {
        private readonly List<Active> actives = new List<Active>();
        private readonly List<Event> events = new List<Event>();
        private readonly List<Passive> passives = new List<Passive>();

        public void Command(string trigger, Func<Envelope, Task> func) => actives.Add(new Active(trigger, func));
        public void Passive(string pattern, Func<Envelope, Task> func) => passives.Add(new Passive(pattern, func));
        public void Event(string command, Func<Message, Task> func) => events.Add(new Event(command, func));

        public async void Dispatch(Message msg) {
            var tasks = new List<Task>();

            // try user-triggered commands first
            if (msg.Command == "PRIVMSG") {
                // try all of the !commands first.
                tasks.AddRange(CollectActives(msg));

                // then the regex commands
                tasks.AddRange(CollectPassives(msg));
            }

            // finally the catch all matched events
            tasks.AddRange(CollectEvents(msg));

            await Task.WhenAll(tasks);
        }

        private IEnumerable<Task> CollectActives(Message msg) {
            return
                // for each active
                from active in actives
                    // filter to !command
                    .Select(active => new {trigger = "!" + active.Trigger, action = active.Action})
                    // find the ones that match
                    .Where(active => msg.Data.StartsWith(active.trigger))
                // create the param
                let param = msg.Data.Substring(active.trigger.Length).Trim()
                // create the envelope
                let env = new Envelope(msg, string.IsNullOrWhiteSpace(param) ? null : param)
                // create the wrapped task
                select Task.Run(async () => {
                        Log.Debug("running action: {trigger}", active.trigger);
                        await active.action(env);
                    }
                ).ContinueWith(t => {
                    t?.Exception?.Flatten().Handle(ex => {
                        Log.Warning(ex, "unhandled exception for {trigger}", active.trigger);
                        return true;
                    });
                });
        }

        private IEnumerable<Task> CollectPassives(Message msg) {
            return
                // for each passive
                from passive in passives
                    // filter matching regex
                    .Where(passive => passive.Pattern.IsMatch(msg.Data))
                // create matches
                let matches = new Matches(passive.Pattern, msg.Data)
                // create the envelople
                let env = new Envelope(msg, matches: matches)
                // create the wrapped task
                select Task.Run(async () => {
                    Log.Debug("running passive: {pattern}", passive.Pattern);
                    await passive.Action(env);
                }).ContinueWith(t => {
                    t?.Exception?.Flatten().Handle(ex => {
                        Log.Warning(ex, "unhandled exception for {pattern}", passive.Pattern.ToString());
                        return true;
                    });
                });
        }

        private IEnumerable<Task> CollectEvents(Message msg) {
            return events
                // filter matching commands
                .Where(ev => ev.Command == msg.Command)
                // then create a list of wrapped tasks
                .Select(evnt => Task.Run(async () => {
                    Log.Debug("running event: {event}", evnt.Command);
                    await evnt.Action(msg);
                }).ContinueWith(t => {
                    t?.Exception?.Flatten().Handle(ex => {
                        Log.Warning(ex, "unhandled exception for {@msg}", msg);
                        return true;
                    });
                }));
        }
    }
}