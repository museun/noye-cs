namespace Noye.Modules {
    public class BigFile : Module {
        private const string PATTERN =
            @"(?<link>(:?[^\s]+)(:?bigfile|attach)\.mail\.(:?daum|naver)\.(:?net|com)\/bigfile(:?upload)?[^\s]+)";

        public BigFile(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(PATTERN, async env => {
                foreach (var link in env.Matches.Get("link")) {
                    var headers = await HttpExtensions.GetHeaders(link);
                    var filename = headers?.ContentDisposition.FileNameStar;
                    if (!string.IsNullOrWhiteSpace(filename)) {
                        await Noye.Say(env, filename);
                    }
                }
            });
        }
    }
}