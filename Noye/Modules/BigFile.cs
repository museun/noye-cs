namespace Noye.Modules {
    public class BigFile : Module {
        private const string PATTERN =
            @"(?<link>(:?[^\s]+)(:?bigfile|attach)\.mail\.(:?daum|naver)\.(:?net|com)\/bigfile(:?upload)?[^\s]+)";

        public BigFile(INoye noye) : base(noye) { }

        public override void Register() {
            Noye.Passive(this, PATTERN, async env => {
                await WithContext(env, "fiilename was empty").TryEach("link", async (link, ctx) => {
                    var headers = await HttpExtensions.GetHeaders(link);
                    var filename = headers?.ContentDisposition?.FileNameStar;
                    await Noye.Say(env, filename, ctx);
                });
            });
        }
    }
}