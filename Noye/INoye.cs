namespace Noye {
    using System;
    using System.Threading.Tasks;

    public interface INoye {
        Task Say(Envelope env, string data);
        Task Reply(Envelope env, string data);
        Task Emote(Envelope env, string msg);
        Task Raw(string data);

        void Command(string trigger, Func<Envelope, Task> func);
        void Passive(string pattern, Func<Envelope, Task> func);
        void Event(string command, Func<Message, Task> func);

        Task<bool> CheckAuth(Envelope env);

        string GetHostAddress();
        T Resolve<T>();
    }
}