namespace Noye {
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Nett;
    using Serilog.Events;

    public class Configuration {
        public LogEventLevel LogLevel { get; set; } = LogEventLevel.Debug;
        public ServerConfig Server { get; set; } = new ServerConfig();
        public UserConfig User { get; set; } = new UserConfig();
        public HttpConfig Http { get; set; } = new HttpConfig();
        public RestartConfig Restart { get; set; } = new RestartConfig();
        public List<string> DisabledModules { get; set; } = new List<string>();
        public ModuleConfig Module { get; set; } = new ModuleConfig();

        public static Configuration Load() => Toml.ReadFile<Configuration>("noye.toml");
        public static void Save(Configuration conf) => Toml.WriteFile(conf, "noye.toml");
    }

    public class ServerConfig {
        public string Address { get; set; } = "localhost";
        public int Port { get; set; } = 6667;
        public string[] Owners { get; set; } = default(string[]);
        public string[] Autojoin { get; set; } = default(string[]);
        public QuakenetConfig Quakenet { get; set; } = new QuakenetConfig();
    }

    public class UserConfig {
        public string Nickname { get; set; } = "noye";
        public string Username { get; set; } = "noye";
        public string Realname { get; set; } = "noye in C#!";
    }

    public class HttpConfig {
        public string Bind { get; set; } = "localhost";
        public int Port { get; set; } = 2222;
    }

    public class RestartConfig {
        public string Address { get; set; } = "localhost";
        public int Port { get; set; } = 54145;
    }

    public class QuakenetConfig {
        public string Username { get; set; } = default(string);
        public string Password { get; set; } = default(string);
    }
    
    public class ModuleConfig {
        public YoutubeConfig Youtube { get; internal set; } = new YoutubeConfig();
        public GDriveConfig GDrive { get; internal set; } = new GDriveConfig();
        public SendAnywhereConfig SendAnywhere { get; internal set; } = new SendAnywhereConfig();
        public ImgurConfig Imgur { get; internal set; } = new ImgurConfig();
        public TwitchConfig Twitch { get; internal set; } = new TwitchConfig();
        public PicturesConfig Pictures { get; internal set; } = new PicturesConfig();

        public static T Get<T>() where T : class, IModuleConfig {
            var conf = Configuration.Load();
            var type = conf.Module.GetType();
            var fields = type.GetRuntimeFields();
            var field = fields.First(f => f.FieldType == typeof(T));
            return field.GetValue(conf.Module) as T;
        }
    }
    
    public interface IModuleConfig { }

    public class ApiKeyConfig : IModuleConfig {
        public string ApiKey { get; set; } = default(string);

        public static string Get<T>() where T : ApiKeyConfig {
            var conf = ModuleConfig.Get<T>();
            if (conf == null || string.IsNullOrWhiteSpace(conf.ApiKey)) {
                throw new Module.MissingApiKeyException();
            }

            return conf.ApiKey;
        }
    }

    public class YoutubeConfig : ApiKeyConfig { }

    public class GDriveConfig : ApiKeyConfig { }

    public class SendAnywhereConfig : ApiKeyConfig { }

    public class ImgurConfig : ApiKeyConfig { }

    public class TwitchConfig : ApiKeyConfig { }

    public class MarkovConfig : IModuleConfig {
        public string Address { get; set; } = "http://localhost:7878";
        public Dictionary<string, float> ResponseRate { get; set; }
    }

    public class PicturesConfig : IModuleConfig {
        public Dictionary<string, Item> Directorties { get; internal set; } = default(Dictionary<string, Item>);

        public class Item {
            public string Directory { get; set; } = default(string);
            public string Command { get; set; } = default(string);
            public int Chance { get; set; } = 20;
            public string[] BannedChannels { get; set; } = default(string[]);
        }
    }
}