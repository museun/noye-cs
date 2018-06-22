namespace Noye {
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Autofac;
    using Serilog;

    public class Bot : Dispatcher, INoye {
        private readonly string address;
        private readonly IContainer container;
        private readonly Proto proto;

        public Bot(Proto proto, IContainer container) {
            this.proto = proto;
            this.container = container;

            var addr = Utilities.GetIpAddress();
            if (string.IsNullOrWhiteSpace(addr)) {
                Log.Error("cannot get external IP address");
            }
            else {
                address = addr + ":" + Configuration.Load().Http.Port;
            }
        }

        public async Task Say(Envelope env, string data, Context ctx) {
            if (!string.IsNullOrWhiteSpace(data)) {
                await proto.Privmsg(env.Target, data);
                Log.Debug("({class}) [{channel}] saying '{data}'", ctx?.Name ?? "unknown", env.Target, data);
            }
            else Warn(ctx);
        }

        public async Task Reply(Envelope env, string data, Context ctx = null) {
            if (!string.IsNullOrWhiteSpace(data)) {
                await proto.Privmsg(env.Target, $"{env.Sender}: {data}");
                Log.Debug("({class}) [{name} @ {channel}] reply '{data}'", ctx?.Name ?? "unknown", env.Sender,
                    env.Target, data);
            }
            else Warn(ctx);
        }

        public async Task Emote(Envelope env, string msg, Context ctx = null) {
            if (!string.IsNullOrWhiteSpace(msg)) await proto.Action(env.Target, msg);
            else Warn(ctx);
        }

        public async Task Raw(string data) => await proto.Send(data);

        public string GetHostAddress() => Debugger.IsAttached ? "localhost:2222" : address;

        public T Resolve<T>() => container.Resolve<T>();

        public async Task<bool> CheckAuth(Envelope env) {
            var conf = Configuration.Load();
            if (conf.Server.Owners.Contains(env.Sender)) return true;
            await Emote(env, "checks something");
            await Reply(env, "you cannot do that");
            return false;
        }

        private static void Warn(Context ctx) {
            if (ctx != null) {
                Log.Warning("{ctx}", ctx);
            }
        }
    }
}