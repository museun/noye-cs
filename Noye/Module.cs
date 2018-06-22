namespace Noye {
    using System;

    public abstract class Module : IDisposable {
        protected HttpClient httpClient = new HttpClient();

        protected Module(INoye noye) {
            Noye = noye;
        }

        protected INoye Noye { get; }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public abstract void Register();

        protected virtual void Dispose(bool disposing) {
            if (disposing) httpClient.Dispose();
        }

        protected Context WithContext(Envelope env, string msg) =>
            new Context {
                Envelope = env,
                Message = msg,
                Sender = env.Sender,
                Target = env.Target,
                Name = GetType().Name
            };

        public class MissingApiKeyException : Exception { }

        public class CreationException : Exception { }
    }
}