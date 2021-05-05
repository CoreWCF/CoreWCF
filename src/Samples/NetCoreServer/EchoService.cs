using System.Threading.Tasks;
using Contract;

namespace NetCoreServer
{
    public class EchoService : Contract.IEchoService
    {
        public string Echo(string text)
        {
            System.Console.WriteLine($"Received {text} from client!");
            return text;
        }

        public string ComplexEcho(EchoMessage text)
        {
            System.Console.WriteLine($"Received {text.Text} from client!");
            return text.Text;
        }

        public async Task<string> EchoStringAsync(string echo)
        {
            System.Console.WriteLine($"Received {echo} from client!");
            await Task.Yield();
            return echo;
        }
    }
}
