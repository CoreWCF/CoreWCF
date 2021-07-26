using System.ServiceModel;

namespace DesktopServer
{
    public class EchoService : Contract.IEchoService
    {
        public string Echo(string text)
        {
            System.Console.WriteLine($"Received {text} from client!");
            return text;
        }

        public string ComplexEcho(Contract.EchoMessage text)
        {
            System.Console.WriteLine($"Received {text.Text} from client!");
            return text.Text;
        }

        public string FailEcho(string text)
        {
            System.Console.WriteLine($"Received {text} and Fault generated on client!");
            throw new FaultException<Contract.EchoFault>(new Contract.EchoFault() { Text = "CoreWCF Fault OK" });
        }
    }
}
