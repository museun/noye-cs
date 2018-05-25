namespace Noye {
    using System;
    using System.IO;
    using Autofac;
    using Modules;
    using Nancy.Bootstrapper;
    using Nancy.Bootstrappers.Autofac;
    using Nett;
    using Serilog;

    internal class NancyBootstrapper : AutofacNancyBootstrapper {
        private readonly IContainer container;

        public NancyBootstrapper(IContainer container) {
            this.container = container;
        }

        //protected override DiagnosticsConfiguration DiagnosticsConfiguration =>
        //    new DiagnosticsConfiguration {Password = "password"};

        protected override ILifetimeScope GetApplicationContainer() => container;

        protected override void ApplicationStartup(ILifetimeScope con, IPipelines pipelines) {
            pipelines.AfterRequest += ctx => {
                ctx.CheckForIfNonMatch();
                ctx.CheckForIfModifiedSince();
            };

            base.ApplicationStartup(con, pipelines);
        }
    }

    public class NoyeModule : Autofac.Module {
        protected override void Load(ContainerBuilder builder) {
            builder.RegisterType<MemoryServe>().SingleInstance();
            builder.RegisterType<PictureServe>().SingleInstance();
            builder.RegisterType<NancyBootstrapper>().SingleInstance();
            builder.RegisterType<HttpServer>().SingleInstance();
        }
    }

    public static class Program {
        private static void Main(string[] args) {
            const string CONFIG_FILE = "noye.toml";
            if (!File.Exists(CONFIG_FILE)) {
                new LoggerConfiguration().WriteTo.Console()
                    .CreateLogger()
                    .Error($"{CONFIG_FILE} does not exist. creating default one");
                Toml.WriteFile(new Configuration(), CONFIG_FILE);
                Console.ReadLine();
                return;
            }

            var config = Toml.ReadFile<Configuration>(CONFIG_FILE);
            var logconf = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(
                    "noye.log",
                    shared: true,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 2
                ).MinimumLevel.Is(config.LogLevel);
            Log.Logger = logconf.CreateLogger();

            try {
                new Noye(config).Run();
            }
            catch (IrcClient.CannotConnectException ex) {
                Log.Error(ex, "cannot connect");
            }
            catch (Exception ex) {
                Log.Error(ex, "unknown error");
            }
        }
    }
}