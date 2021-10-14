using Contract;
using System;
using System.Threading.Tasks;
#if NETFRAMEWORK
using System.ServiceModel;
#else
using CoreWCF;
#endif

namespace ServerLogic
{
    public class EchoService : IEchoService
    {
        private FaultException CreateFault<T>(T detail, string reason, string code)
        {
#if NETFRAMEWORK 
            return new System.ServiceModel.FaultException<T>(detail, new FaultReason(reason), new FaultCode(code));
#else
            return new CoreWCF.FaultException<T>(detail, new FaultReason(reason), new FaultCode(code));
#endif
        }

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

        public string FailEcho(string text)
            => throw CreateFault(new EchoFault() { Text = "WCF Fault OK" }, "FailReason", "FaultCode");

#if !NETFRAMEWORK
        [AuthorizeRole("CoreWCFGroupAdmin")]
        public string EchoForPermission(string echo)
        {
            return echo;
        }
#endif

    }
}
