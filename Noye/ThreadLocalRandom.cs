namespace Noye {
    using System;
    using System.Threading;

    public static class ThreadLocalRandom {
        private static readonly Random local = new Random();
        private static readonly object locker = new object();
        private static readonly ThreadLocal<Random> instance = new ThreadLocal<Random>(NewRandom);
        public static Random Instance => instance.Value;

        public static Random NewRandom() {
            lock (locker) return new Random(local.Next());
        }

        public static int Next(int max) => Instance.Next(max);
        public static int Next(int min, int max) => Instance.Next(min, max);
    }
}