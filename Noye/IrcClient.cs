namespace Noye {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;

    public class IrcClient : Proto, IDisposable {
        private readonly TcpClient client = new TcpClient {SendTimeout = 15};
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private readonly object locker = new object();
        private readonly StreamWriter writer;

        public IrcClient(Config config) {
            try {
                Log.Information("connecting to {Address}:{Port}", config.Address, config.Port);
                client.Connect(config.Address, config.Port);
            }
            catch (SocketException ex) {
                throw new CannotConnectException($"{config.Address}:{config.Port}", ex);
            }

            new Thread(async obj => {
                var cancel = (CancellationToken) obj;
                lock (locker) {
                    LastPing = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }

                const int timeout = 30;
                do {
                    await Task.Delay(TimeSpan.FromSeconds(timeout), cancel)
                        .ContinueWith(t => { });

                    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    lock (locker) {
                        if (LastPing > now + timeout * 1.5) {
                            // ping timeout
                            Log.Warning("detected a ping timeout");
                            client.Close();
                            Dispose();
                        }
                    }

                    if (!cancel.IsCancellationRequested) {
                        await Send($"PING {now}");
                    }
                }
                while (!cancel.IsCancellationRequested);
            }).Start(cts.Token);

            writer = new StreamWriter(client.GetStream());
            Register(config.Nick, config.User, config.Real).Wait();
            Log.Information("connected to server");
        }

        private long LastPing { get; set; }

        public void Dispose() {
            cts.Cancel();
            client.Close();
            client?.Dispose();
            writer?.Dispose();
        }

        public override async Task Send(string raw) {
            if (string.IsNullOrWhiteSpace(raw)) {
                Log.Warning("tryied to send an empty line");
                return;
            }

            foreach (var line in Split(raw)) {
                try {
                    Log.Verbose("> {line}", line.Trim());
                    await writer.WriteAsync(line);
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex) {
                    Log.Error(ex, "unexpected exception in Send");
                }
            }

            await writer.FlushAsync();
        }

        public IEnumerable<Message> YieldMessages() {
            var reader = new StreamReader(client.GetStream());
            while (!cts.IsCancellationRequested && !reader.EndOfStream) {
                string line;
                try {
                    if (cts.IsCancellationRequested) {
                        yield break;
                    }

                    line = reader.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(line)) {
                        yield break;
                    }

                    line = line.Trim();
                }
                catch (IOException) {
                    Log.Verbose("caught io exception in message loop, breaking.");
                    yield break;
                }

                Log.Verbose("< {line}", line);

                var msg = new Message(line);
                if (msg.Command == "PONG") {
                    lock (locker) {
                        if (long.TryParse(msg.Data, out var ping)) {
                            LastPing = ping;
                        }
                        else {
                            Log.Warning("got an invalid PONG response: {msg}", msg);
                        }
                    }
                }

                yield return msg;
            }
        }

        private static IEnumerable<string> Split(string raw) {
            const int MAX = 510;
            if (raw.Length <= MAX || !raw.Contains(":")) {
                return new List<string> {$"{raw}\r\n"};
            }

            var split = raw.Split(new[] {':'}, 2).Select(e => e.Trim()).ToList();
            var (head, tail) = (split[0], split[1]);
            return tail.SplitAt(MAX - head.Length - 2).Select(part => $"{head} :{part}\r\n");
        }

        public async Task Close() {
            var tasks = new[] {
                Send("QUIT :bye"),
                Task.Run(() => {
                    cts.Cancel();
                    client.Close();
                    client?.Dispose();
                    writer?.Dispose();
                })
            };
            Log.Information("closing the irc connection");
            await Task.WhenAll(tasks);
        }

        public class Config {
            public string Address { get; set; }
            public int Port { get; set; }
            public string Nick { get; set; }
            public string User { get; set; }
            public string Real { get; set; }
        }

        public class CannotConnectException : Exception {
            public CannotConnectException(string address, Exception inner)
                : base($"cannot connect to {address}", inner) { }
        }
    }
}