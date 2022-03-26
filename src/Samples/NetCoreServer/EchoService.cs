using Microsoft.Extensions.Logging;

#if NETFRAMEWORK
using System.ServiceModel;
#else
using CoreWCF;
#endif

using Contract;

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

        private readonly ILogger<EchoService> _logger;

        public EchoService()
        {
        }

        public EchoService(ILogger<EchoService> logger)
        {
            _logger = logger;
        }

        private void Log(string args,
                [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            string message = $"Received {memberName} : {args}";
            if (_logger != null)
            {
                _logger.LogInformation(message);
                return;
            }
            System.Console.WriteLine(message);
        }

        public string Echo(string text)
        {
            Log(text);
            return text;
        }

        public string ComplexEcho(EchoMessage text)
        {
            Log(text.Text);
            return text.Text;
        }

        public string FailEcho(string text)
        {
            Log(text);
            throw CreateFault(new EchoFault() { Text = "WCF Fault OK" }, "FailReason", "FaultCode");
        }

#if !NETFRAMEWORK
        [AuthorizeRole("CoreWCFGroupAdmin")]
        public string EchoForPermission(string echo)
        {
            Log(echo);
            return echo;
        }
#endif

    }
}
