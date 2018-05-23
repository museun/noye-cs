namespace Noye.Modules {
    using System;
    using System.Data;
    using System.Data.SQLite;
    using System.Linq;
    using System.Threading.Tasks;
    using Dapper;

    public class Repost : Module {
        private static readonly string[] IGNORED = {"twitch.tv"}; // TODO store this in the db

        private readonly IDbConnection
            db = new SQLiteConnection("Data Source=noye.db;Version=3;Pooling=True;Max Pool Size=100");

        public Repost(INoye noye) : base(noye) {
            const string TABLE = "CREATE TABLE IF NOT EXISTS `links` (" +
                                 "`link` TEXT NOT NULL," +
                                 "`time` TEXT NOT NULL," +
                                 "`nick` TEXT NOT NULL," +
                                 "`room` TEXT NOT NULL," +
                                 "`posts` INTEGER," +
                                 "`ignored` INTEGER," +
                                 "UNIQUE(link, room))";

            db.Execute(TABLE);
        }

        public override void Register() {
            Noye.Passive(@"(?<link>(?:www|https?)[^\s]+)", async env => {
                foreach (var link in env.Matches.Get("link")) {
                    var msg = await Shame(link, env.Sender, env.Target);
                    if (!string.IsNullOrWhiteSpace(msg)) {
                        await Noye.Say(env, msg);
                    }
                }
            });
        }

        private async Task<string> Shame(string link, string sender, string target) {
            var url = AsUrl(link);
            // this is not a valid url
            if (url == null) {
                return null;
            }

            // the url is on the ignore list
            if (IGNORED.Any(s => url.Contains(s))) {
                return null;
            }

            var prev = await TryAdd(url, sender, target);
            // this is the first time its been seen
            if (prev == null) {
                return null;
            }

            var time = DateTime.Now - prev.Time;
            // only shame for things within a week
            if (time.TotalDays > 7) {
                return null;
            }

            var nick = prev.Nick;
            if (nick != sender) {
                return $"that was already posted by {nick}. " +
                       $"previously {prev.Posts} times. " +
                       $"last being {time.RelativeTime()} ago ";
            }

            var rel = time.RelativeTime();
            return rel != "just now"
                ? $"didn't you link that {rel} ago? ({prev.Posts} times prior)"
                : $"didn't you link that {rel}? ({prev.Posts} times prior)";
        }

        private static string AsUrl(string input) {
            try {
                return new Uri(input).ToString();
            }
            catch (UriFormatException) {
                return null;
            }
        }

        private async Task<LinkItem> TryAdd(string link, string nick, string room) {
            const string UPDATE = "UPDATE links SET " +
                                  "link = @link," +
                                  "posts = @posts," +
                                  "nick = @nick," +
                                  "time = @time," +
                                  "ignored = @ignored " +
                                  "WHERE link = @old " +
                                  "AND room = @room";

            const string CREATE = "INSERT INTO links (link, nick, room, time, posts, ignored) " +
                                  "VALUES (@link, @nick, @room, @time, @posts, @ignored)";

            var result = await GetLinkFromChannel(link, room);
            if (result != null) {
                await db.ExecuteAsync(UPDATE, new {
                    link,
                    nick,
                    room,
                    time = DateTime.Now,
                    posts = result.Posts + 1,
                    ignored = 0,
                    old = result.Link
                });
                return result;
            }
            
            await db.ExecuteAsync(CREATE, new {
                link,
                nick,
                room,
                time = DateTime.Now,
                posts = 1,
                ignored = 0
            });
            return null;
        }

        private async Task<LinkItem> GetLinkFromChannel(string link, string room) {
            const string QUERY = "SELECT * FROM links " +
                                 "WHERE link = @link " +
                                 "AND room = @room";

            var q = await db.QueryAsync<LinkItem>(QUERY, new {room, link});
            return q.FirstOrDefault();
        }

        public class LinkItem {
            public string Link { get; set; }
            public string Nick { get; set; }
            public string Room { get; set; }
            public DateTime Time { get; set; }
            public int Posts { get; set; }
            public int Ignored { get; set; }
        }
    }
}