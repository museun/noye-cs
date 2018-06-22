namespace Noye {
    using System;
    using System.Threading.Tasks;

    public interface INoye {
        Task Say(Envelope env, string data, Context ctx = null);
        Task Reply(Envelope env, string data, Context ctx = null);
        Task Emote(Envelope env, string msg, Context ctx = null);
        Task Raw(string data);

        void Command(Module module, string trigger, Func<Envelope, Task> func);
        void Passive(Module module, string pattern, Func<Envelope, Task> func);
        void Event(Module module, string command, Func<Message, Task> func);

        Task<bool> CheckAuth(Envelope env, Context ctx = null);

        string GetHostAddress();
        T Resolve<T>();
    }
}