using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;

namespace ClientContract
{
    internal static class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string TESTSERVICE_NAME = nameof(ITestService);
        public const string OPERATION_BASE = NS + TESTSERVICE_NAME + "/";
    }

    [ServiceContract(Namespace = "http://tempuri.org/", Name = Constants.TESTSERVICE_NAME)]
    public interface ITestService
    {
        [OperationContract(Name = "Echo", Action = Constants.OPERATION_BASE + "Echo",
            ReplyAction = Constants.OPERATION_BASE + "EchoResponse")]
        string EchoString(string echo);

        [OperationContract(Name = "WaitForSecondRequest", Action = Constants.OPERATION_BASE + "WaitForSecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "WaitForSecondRequestResponse")]
        Task<bool> WaitForSecondRequestAsync();

        [OperationContract(Name = "SecondRequest", Action = Constants.OPERATION_BASE + "SecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "SecondRequestResponse")]
        void SecondRequest();
    }
}