namespace Noye.Modules {
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    public class Builtin : Module {
        public Builtin(INoye noye) : base(noye) { }

        public override void Register() {
            // when we get a PING, response a with a PONG
            Noye.Event(this, "PING", async msg => await Noye.Raw($"PONG {msg.Data}"));

            // join any channel we're invited to
            Noye.Event(this, "INVITE", async msg => await Noye.Raw($"JOIN {msg.Data}"));

            // if our nick is in used, append _ to it and try again
            Noye.Event(this, "433", async msg => await Noye.Raw($"NICK {msg.Parameters[1]}_"));

            // reclamin our nick if someone spotted with it quits.
            Noye.Event(this, "QUIT", async msg => {
                var me = Configuration.Load().User.Nickname;
                if (me == msg.Prefix.Split('!').FirstOrDefault()) {
                    await Noye.Raw($"NICK {me}");
                }
            });

            // auth with Q, and join channels when we finally connect
            Noye.Event(this, "001", async msg => {
                var conf = Configuration.Load();
                if (conf.Server.Quakenet != null) {
                    var username = conf.Server.Quakenet.Username;
                    var password = conf.Server.Quakenet.Password;
                    await Noye.Raw($"PRIVMSG Q@CServe.quakenet.org :AUTH {username} {password}");
                    await Noye.Raw($"MODE {msg.Parameters.First()} +x");
                }

                if (conf.Server.Autojoin.Any()) {
                    var channels = string.Join(",", conf.Server.Autojoin);
                    await Noye.Raw($"JOIN {channels}");
                }
            });

            // join any requested channel
            Noye.Command(this, "join", async env => {
                if (env.Param != null) {
                    await Noye.Raw($"JOIN {env.Param}");
                }
            });

            // part the current channel
            Noye.Command(this, "part", async env => await Noye.Raw($"PART {env.Target}"));

            // get the bots uptime
            var start = DateTime.Now;
            Noye.Command(this, "uptime", async env => {
                var time = DateTime.Now - start;
                await Noye.Reply(env, time.RelativeTime());
            });

            // restart the bot 
            Noye.Command(this, "restart", async env => {
                if (!await Noye.CheckAuth(env)) {
                    return;
                }

                var conf = Configuration.Load();
                using (var client = new TcpClient(conf.Restart.Address, conf.Restart.Port)) {
                    using (var sw = new StreamWriter(client.GetStream())) {
                        sw.Write("RESTART\0");
                    }
                }
            });

            // set the respawn relay
            Noye.Command(this, "respawn", async env => {
                if (!await Noye.CheckAuth(env)) {
                    return;
                }

                var conf = Configuration.Load();
                var delay = env.Param ?? "15";
                using (var client = new TcpClient(conf.Restart.Address, conf.Restart.Port)) {
                    using (var sw = new StreamWriter(client.GetStream())) {
                        sw.Write($"DELAY {delay}\0");
                    }
                }
            });

            // simulates a crash.
            Noye.Command(this, "crash", async env => {
                if (!await Noye.CheckAuth(env)) {
                    return;
                }

                await Noye.Reply(env, "simulating a crash in 5 seconds..");
                await Task.Delay(TimeSpan.FromSeconds(5));
                await Noye.Reply(env, "throwing an exception");
                throw new Exception("simulating a crash");
            });

            // send the latest log
            Noye.Command(this, "send logs", async env => {
                if (!await Noye.CheckAuth(env)) {
                    return;
                }

                var dir = AppDomain.CurrentDomain.BaseDirectory;
                var files = Directory.EnumerateFiles(dir, "*.log", SearchOption.TopDirectoryOnly);
                var recent = files.OrderBy(File.GetLastWriteTime).Last();
                if (recent == null) {
                    await Noye.Reply(env, "I can't find any logs");
                    return;
                }

                using (var fs = new FileStream(recent, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs)) {
                    var data = await sr.ReadToEndAsync();
                    var item = new MemoryServe.Item(
                        Path.GetFileName(recent), // name of the file
                        MemoryServe.PlainText,    // the content type
                        data,                     // file data
                        TimeSpan.FromSeconds(30)  // how long it should be available for
                    );
                    var id = Noye.Resolve<MemoryServe>().Store(item);
                    var host = Noye.GetHostAddress();
                    await Noye.Raw($"PRIVMSG {env.Sender} :http://{host}/s/{id}");
                }
            });
        }
    }
}