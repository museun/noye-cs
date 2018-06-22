namespace Noye {
    using System.Threading.Tasks;

    public abstract class Proto {
        public async Task Privmsg(string target, string data) {
            await Send($"PRIVMSG {target} :{data}");
        }

        public async Task Notice(string target, string data) {
            await Send($"NOTICE {target} :{data}");
        }

        public async Task Action(string target, string data) {
            await Send($"PRIVMSG {target} :{(char)1}ACTION {data}{(char)1}");
        }

        public async Task Register(string nick, string user, string real) {
            await Send($"NICK {nick}");
            await Send($"USER {user} * 8 :{real}");
        }

        public abstract Task Send(string raw);
    }
}