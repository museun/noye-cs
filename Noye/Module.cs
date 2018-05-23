namespace Noye {
    using System;

    public abstract class Module : IDisposable {
        protected readonly HttpClient httpClient = new HttpClient();

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

        public class MissingApiKeyException : Exception { }

        public class CreationException : Exception { }
    }
}