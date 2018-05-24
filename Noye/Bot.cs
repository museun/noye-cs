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

        public async Task Say(Envelope env, string data) {
            if (!string.IsNullOrWhiteSpace(data)) await proto.Privmsg(env.Target, data);
        }

        public async Task Reply(Envelope env, string data) {
            if (!string.IsNullOrWhiteSpace(data)) await proto.Privmsg(env.Target, $"{env.Sender}: {data}");
        }

        public async Task Emote(Envelope env, string msg) => await proto.Action(env.Target, msg);
        public async Task Raw(string data) => await proto.Send(data);

        public string GetHostAddress() => Debugger.IsAttached ? "localhost:2222" : address;

        public T Resolve<T>() => container.Resolve<T>();

        public async Task<bool> CheckAuth(Envelope env) {
            var conf = Configuration.Load();
            if (conf.Server.Owners.Contains(env.Sender)) return true;
            await Reply(env, "you cannot do that");
            return false;
        }
    }
}