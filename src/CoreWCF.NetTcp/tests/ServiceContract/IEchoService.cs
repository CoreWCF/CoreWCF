using System.IO;
using System.Threading.Tasks;
using CoreWCF;

namespace ServiceContract
{
    internal static class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string ECHOSERVICE_NAME = nameof(IEchoService);
        public const string OPERATION_BASE = NS + ECHOSERVICE_NAME + "/";
    }

    [ServiceContract(Namespace = Constants.NS, Name = Constants.ECHOSERVICE_NAME)]
    public interface IEchoService
    {
        [OperationContract(Name = "Echo", Action = Constants.OPERATION_BASE + "Echo",
            ReplyAction = Constants.OPERATION_BASE + "EchoResponse")]
        string EchoString(string echo);

        //[OperationContract(Name = "EchoStream", Action = Constants.OPERATION_BASE + "EchoStream",
        //    ReplyAction = Constants.OPERATION_BASE + "EchoStreamResponse")]
        //Stream EchoStream(Stream echo);

        //[OperationContract(Name = "EchoStringAsync", Action = Constants.OPERATION_BASE + "EchoStringAsync",
        //    ReplyAction = Constants.OPERATION_BASE + "EchoStringAsyncResponse")]
        //Task<string> EchoStringAsync(string echo);

        //[OperationContract(Name = "EchoStreamAsync", Action = Constants.OPERATION_BASE + "EchoStreamAsync",
        //    ReplyAction = Constants.OPERATION_BASE + "EchoStreamAsyncResponse")]
        //Task<Stream> EchoStreamAsync(Stream echo);
    }
}