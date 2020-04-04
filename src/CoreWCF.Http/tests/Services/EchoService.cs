using System.IO;
using System.Threading.Tasks;

namespace Services
{
    public class EchoService : ServiceContract.IEchoService
    {
        public string EchoString(string echo)
        {
            return echo;
        }

        public Stream EchoStream(Stream echo)
        {
            var stream = new MemoryStream();
            echo.CopyTo(stream);
            stream.Position = 0;
            return stream;
        }

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