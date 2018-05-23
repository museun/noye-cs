namespace Noye.Modules {
    using System.Threading.Tasks;

    public class Twitch : Module {
        public Twitch(INoye noye) : base(noye) {
            var key = ApiKeyConfig.Get<TwitchConfig>();
            httpClient.DefaultRequestHeaders.Add("Client-ID", key);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.twitchtv.v5+json");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "noye/1.2");
        }

        public override void Register() {
            Noye.Passive(@"twitch\.tv\/(?!videos)(?<stream>\w+)", async env => {
                foreach (var name in env.Matches.Get("stream")) {
                    await Lookup(env, name);
                }
            });
        }

        // @formatter:off
        private async Task Lookup(Envelope env, string name) {
            var result = await httpClient.GetAnonymous(getLogin(name), new {
                users = new[] { new { _id = "", display_name = "", name = "" } }
            });

            if (result.users.Length < 1) {
                // not a valid user
                return;
            }

            var uid = result.users[0]._id;
            var stream = await httpClient.GetAnonymous(getStream(uid), new {
                data = new[] { new { user_id = "", game_id = "", title = "" } }
            });

            if (stream.data.Length < 1) {
                await Noye.Say(env, $"{name} is offline");
                return;
            }

            var gid = stream.data[0].game_id;
            var game = await httpClient.GetAnonymous(getGames(gid), new {
                data = new[] { new { name = "" } }
            });

            var game_name = "no game";
            if (game.data.Length > 0) {
                game_name = game.data[0].name;
            }

            var status = stream.data[0].title == "" ? "no status" : stream.data[0].title;
            await Noye.Say(env, $"{result.users[0].display_name} streaming '{game_name}' -- {status}");
        }
        // @formatter:on

        private static string getLogin(string name) {
            return "https://api.twitch.tv/kraken/users?login=" + name;
        }

        private static string getStream(string id) {
            return "https://api.twitch.tv/helix/streams?user_id=" + id;
        }

        private static string getGames(string id) {
            return "https://api.twitch.tv/helix/games?id=" + id;
        }
    }
}