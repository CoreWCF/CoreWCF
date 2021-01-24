using Contract;
using System.IO;
using System.Threading.Tasks;

namespace NetCoreServer
{
    public class EchoService : IEchoService
    {
        public async Task<string> EchoStringAsync(string echo)
        {
            await Task.Yield();
            return echo;
        }

        public async Task<Stream> EchoStreamAsync(Stream echo)
        {
            var stream = new MemoryStream();
            await echo.CopyToAsync(stream);
            stream.Position = 0;
            return stream;
        }

        public string EchoToFail(string echo)
        {
            return echo;
        }

    }
}