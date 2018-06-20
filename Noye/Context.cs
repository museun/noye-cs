namespace Noye {
    using System;

    public class Context : ICloneable {
        public string Message { get; set; }
        public string Data { get; set; }
        public string Name { get; set; }
        public string Sender { get; set; }
        public string Target { get; set; }

        public object Clone() => new Context {
            Message = Message,
            Data = Data != null ? string.Copy(Data) : null,
            Name = Name,
            Sender = Sender,
            Target = Target
        };

        public override string ToString() {
            var data = string.IsNullOrWhiteSpace(Data) ? "" : $"'{Data}'";
            return $"({Name}) [{Sender} @ {Target}] failure. reason: {Message} {data}";
        }
    }
}