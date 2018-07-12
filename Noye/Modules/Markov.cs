namespace Noye.Modules {
    public class Markov : Module {
        public Markov(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(this, "noye[:,.?!]?", async env => {
                var data = await httpClient.GetStringAsync("http://localhost:7878/markov/next");
                data = data.Replace("\".", ".");
                await Noye.Say(env, data);
            });
        }
    }
}