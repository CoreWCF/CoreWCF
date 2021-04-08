using Contract;
using CoreWCF;

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

        [AuthorizeRole(AllowedRoles = "CoreWCFGroupAdmin")]
        public string EchoForPermission(string echo)
        {
            return echo;
        }
    }
}
