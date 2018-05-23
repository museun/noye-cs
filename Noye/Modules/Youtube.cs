namespace Noye.Modules {
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Web;
    using System.Xml;
    using Newtonsoft.Json;

    public class Youtube : Module {
        private const string VideoRegex =
            @"(?:youtu\.be\/|youtube.com\/(?:\S*(?:v|video_id)=|v\/|e\/|embed\/))(?<id>[\w\-]{11})";

        private const string ChannelRegex =
            @"(?:youtu\.be\/|youtube.com\/(?:\S*(?:channel\/(?<channel>[\w\-]+))|(?:user\/(?<user>[\w\-]+))))";

        private const string PlaylistRegex =
            @"(?:youtu\.be\/|youtube.com\/(?:\S*(?:p|list)=|p\/|playlist\/|view_play_list\/))(?<id>[\w\-]+)";

        private readonly Client client;

        public Youtube(INoye noye) : base(noye) {
            var key = ApiKeyConfig.Get<YoutubeConfig>();
            client = new Client(key);
        }

        public override void Register() {
            Noye.Passive(VideoRegex, async env => await HearVideo(env));
            Noye.Passive(ChannelRegex, async env => await HearChannel(env));
        }

        private async Task HearVideo(Envelope env) {
            var ids = env.Matches.Get("id");
            var json = await client.Get("videos", new Dictionary<string, string> {
                ["id"] = string.Join(",", ids),
                ["part"] = "statistics,snippet,liveStreamingDetails,contentDetails",
                ["fields"] =
                    "items(id,statistics,liveStreamingDetails," +
                    "snippet(title, channelTitle,channelId,liveBroadcastContent,publishedAt)," +
                    "contentDetails(duration,regionRestriction))"
            });

            var videos = JsonConvert.DeserializeAnonymousType(json, new {
                items = new[] {
                    new {
                        id = default(string),
                        snippet = new {
                            publishedAt = default(string),
                            channelId = default(string),
                            title = default(string),
                            channelTitle = default(string),
                            liveBroadcastContent = default(string)
                        },
                        contentDetails = new {
                            duration = default(string)
                        },
                        statistics = new {
                            viewCount = default(long),
                            likeCount = default(long),
                            dislikeCount = default(long),
                            favoriteCount = default(long),
                            commentCount = default(long)
                        },
                        liveStreamingDetails = new {
                            actualStartTime = default(DateTime),
                            actualEndTime = default(DateTime),
                            scheduledStartTime = default(DateTime),
                            scheduledEndTime = default(DateTime),
                            concurrentViewers = default(long)
                        }
                    }
                }
            });

            foreach (var video in videos.items) {
                var title = video.snippet.title;
                var channel = video.snippet.channelTitle;

                if (video.snippet.liveBroadcastContent == "none") {
                    var duration = XmlConvert.ToTimeSpan(video.contentDetails.duration).AsShortTime();
                    var views = video.statistics.viewCount.WithCommas();
                    await Noye.Say(env, $"{title} | {channel} · {duration} · {views} | https://youtu.be/{video.id}");
                }

                if (video.snippet.liveBroadcastContent == "live") {
                    var viewers = video.liveStreamingDetails.concurrentViewers.WithCommas();
                    await Noye.Say(env,
                        $"(LIVE): {title} | {channel} · {viewers} watching | https://youtu.be/{video.id}");
                }

                if (video.snippet.liveBroadcastContent == "upcoming") {
                    var start = (video.liveStreamingDetails.scheduledStartTime - DateTime.UtcNow).StripMilliseconds()
                        .RelativeTime();
                    await Noye.Say(env, $"(Upcoming: {start}) {title} | {channel} | https://youtu.be/{video.id}");
                }
            }
        }

        private async Task HearChannel(Envelope env) {
            async Task format(string data) {
                var json = JsonConvert.DeserializeAnonymousType(data, new {
                    items = new[] {
                        new {
                            id = default(string),
                            snippet = new {
                                title = default(string),
                                description = default(string),
                                publishedAt = default(DateTime)
                            },
                            statistics = new {
                                viewCount = default(long),
                                commentCount = default(long),
                                videoCount = default(long),
                                subscriberCount = default(long),
                                hiddenSubscriberCount = default(bool)
                            }
                        }
                    }
                });

                foreach (var item in json.items) {
                    var id = item.id;
                    var title = item.snippet.title;
                    var views = item.statistics.viewCount.WithCommas();
                    var videos = item.statistics.videoCount.WithCommas();

                    if (item.statistics.hiddenSubscriberCount) {
                        await Noye.Say(env,
                            $"{title} | {videos} videos. {views} views | https://youtube.com/channel/{id}");
                    }
                    else {
                        var subs = item.statistics.subscriberCount.WithCommas();
                        await Noye.Say(env,
                            $"{title} | {videos} videos. {views} views · {subs} subscribers | https://youtube.com/channel/{id}");
                    }
                }
            }

            foreach (var user in env.Matches.Get("user")) {
                var json = await client.Get("channels", new Dictionary<string, string> {
                    ["forUsername"] = user,
                    ["part"] = "snippet,statistics",
                    ["fields"] = "items(etag,id,snippet(title,description,publishedAt),statistics,status)"
                });

                await format(json);
            }

            var channels = env.Matches.Get("channel");
            if (channels.Count > 0) {
                var json = await client.Get("channels", new Dictionary<string, string> {
                    ["id"] = string.Join(",", channels),
                    ["part"] = "snippet,statistics",
                    ["fields"] = "items(etag,id,snippet(title,description,publishedAt),statistics,status)"
                });
                await format(json);
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            if (disposing) {
                client?.Dispose();
            }
        }

        private class Client : HttpClient {
            private const string BaseUri = "https://www.googleapis.com/youtube/v3/";
            private readonly string apikey;

            public Client(string apikey) {
                this.apikey = apikey;
            }

            public async Task<string> Get(string ep, Dictionary<string, string> data) {
                var builder = new UriBuilder(BaseUri + ep);
                var query = HttpUtility.ParseQueryString(string.Empty);
                query["key"] = apikey;
                foreach (var kv in data) {
                    query[kv.Key] = kv.Value;
                }

                builder.Query = query.ToString();
                return await GetStringAsync(builder.ToString());
            }
        }
    }
}