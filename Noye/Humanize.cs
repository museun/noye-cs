namespace Noye {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class Humanize {
        private static readonly SortedList<double, Func<TimeSpan, string>> offsets =
            new SortedList<double, Func<TimeSpan, string>> {
                {0.75, _ => "less than a minute"},
                {1.5, _ => "about a minute"},
                {45, x => $"{x.TotalMinutes:F0} minutes"},
                {90, x => "about an hour"},
                {1440, x => $"about {x.TotalHours:F0} hours"},
                {2880, x => "a day"},
                {43200, x => $"{x.TotalDays:F0} days"},
                {86400, x => "about a month"},
                {525600, x => $"{x.TotalDays / 30:F0} months"},
                {1051200, x => "about a year"},
                {double.MaxValue, x => $"{x.TotalDays / 365:F0} years"}
            };

        public static string WithCommas(this ulong n)
        {
            return n.ToString("n0");
        }

        public static string WithCommas(this long n) {
            return n.ToString("n0");
        }

        public static string AsShortTime(this TimeSpan ts) {
            var s = ts.ToString();
            if (s.StartsWith("00:") && s.Length > 4) {
                return s.Substring(3);
            }

            return s;
        }

        public static TimeSpan StripMilliseconds(this TimeSpan ts) {
            return new TimeSpan(ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
        }

        public static string FuzzyDate(this DateTime date) {
            var ts = DateTime.Now - date;
            var suffix = ts.TotalMinutes > 0 ? " ago" : " from now";
            ts = new TimeSpan(Math.Abs(ts.Ticks));
            return offsets.First(n => ts.TotalMinutes < n.Key).Value(ts) + suffix;
        }

        public static string AsFileSize(this long size) {
            string[] SIZES = {"B", "KB", "MB", "GB"};
            var order = 0;
            float temp = size;
            while (temp >= 1024 && order + 1 < SIZES.Length) {
                order++;
                temp /= 1024;
            }

            return $"{temp:0.##} {SIZES[order]}";
        }

        public static string RelativeTime(this TimeSpan ts, int max = 3, bool ignore = true) {
            max = Math.Max(Math.Min(max, Enum.GetNames(typeof(TimeSpanElement)).Length), 1);

            var parts = new[] {
                Tuple.Create(TimeSpanElement.Day, ts.Days),
                Tuple.Create(TimeSpanElement.Hour, ts.Hours),
                Tuple.Create(TimeSpanElement.Minute, ts.Minutes),
                Tuple.Create(TimeSpanElement.Second, ts.Seconds)
            };
            if (!ignore) {
                parts = parts.Add(Tuple.Create(TimeSpanElement.Millisecond, ts.Milliseconds));
            }

            var els = parts.SkipWhile(i => i.Item2 <= 0).Take(max).Where(i => i.Item2 != 0).Select(p => {
                var plural = p.Item2 > 1 ? "s" : string.Empty;
                return $"{p.Item2} {p.Item1.ToString().ToLower()}{plural}";
            }).ToArray();

            if (els.Length == 0) {
                return "just now";
            }

            if (els.Length == 1) {
                return string.Join(", ", els);
            }

            var last = els.Last();
            return string.Join(" and ", string.Join(", ", els.Reverse().Skip(1).Reverse()), last);
        }
        
        public static T[] Add<T>(this T[] arr, T item) {
            if (arr == null) {
                return null;
            }

            var result = new T[arr.Length + 1];
            arr.CopyTo(result, 0);
            result[arr.Length] = item;
            return result;
        }

        private enum TimeSpanElement {
            Millisecond,
            Second,
            Minute,
            Hour,
            Day
        }
    }
}