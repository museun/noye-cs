namespace Noye {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Autofac;
    using Nancy.Bootstrappers.Autofac;
    using Serilog;

    public class Noye {
        private readonly Bot bot;
        private readonly IContainer container;
        private readonly IEnumerable<Message> messages;
        private readonly List<Module> modules;

        private Noye() {
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyModules(Assembly.GetExecutingAssembly());
            container = builder.Build();
            container.Update(b => b.RegisterInstance(container).As<IContainer>().SingleInstance());
        }

        public Noye(Configuration config) : this() {
            var client = new IrcClient(new IrcClient.Config {
                Address = config.Server.Address,
                Port = config.Server.Port,
                Nick = config.User.Nickname,
                User = config.User.Username,
                Real = config.User.Realname
            });

            bot = new Bot(client, container);
            modules = CreateModules(bot);

            Console.CancelKeyPress += async (s, e) => {
                Log.Verbose("got a Cancel event");
                await client.Close();

                Log.Information("cleaning up modules");
                foreach (var module in modules) {
                    module.Dispose();
                }

                Log.Information("exiting");
                Log.CloseAndFlush();
            };

            messages = client.YieldMessages();
        }

        public void Run() {
            Log.Information("initializing modules");
            foreach (var module in modules) {
                module.Register();
            }

            using (container.Resolve<HttpServer>()) {
                Log.Information("starting message read loop");
                foreach (var msg in messages) {
                    bot.Dispatch(msg);
                }
                Log.Information("done reading messages");
            }
        }

        private static List<Module> CreateModules(Bot bot) {
            var list = new List<Module>();
            foreach (var module in Assembly.GetAssembly(typeof(Module)).GetTypes().Where(type =>
                type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(Module)))) {
                try {
                    list.Add((Module) Activator.CreateInstance(module, bot));
                    Log.Information("loading module: {type}", module);
                }
                catch (TargetInvocationException ex) {
                    switch (ex.InnerException) {
                        case Module.CreationException _:
                            Log.Warning("failed to create {type}", module);
                            break;
                        case Module.MissingApiKeyException _:
                            Log.Warning("cannot create {type}, no API Key found", module);
                            break;
                    }
                }
            }

            return list;
        }
    }
}