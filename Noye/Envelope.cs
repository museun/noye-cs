namespace Noye {
    using System;
    using System.Linq;

    public class Envelope {
        public Envelope(Message msg, string param = null, Matches matches = null) {
            if (msg.Command != "PRIVMSG") {
                throw new ArgumentException("must be a PRIVMSG", nameof(msg));
            }

            Sender = msg.Prefix.Split('!').First();
            Target = msg.Parameters.First();
            Param = param;
            Matches = matches;
        }

        public string Sender { get; }
        public string Target { get; }
        public string Param { get; }
        public Matches Matches { get; }
    }
}