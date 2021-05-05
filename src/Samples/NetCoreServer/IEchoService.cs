using CoreWCF;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Contract
{
    internal static class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string ECHOSERVICE_NAME = nameof(IEchoService);
        public const string OPERATION_BASE = NS + ECHOSERVICE_NAME + "/";
    }

    [ServiceContract(Namespace = Constants.NS, Name = Constants.ECHOSERVICE_NAME)]
    [DataContract]
    public class EchoFault
    {
        private string text;

        [DataMember]
        public string Text
        {
            get { return text; }
            set { text = value; }
        }
    }

    [ServiceContract]
    public interface IEchoService
    {
        [OperationContract(Name = "EchoString", Action = Constants.OPERATION_BASE + "EchoString",
            ReplyAction = Constants.OPERATION_BASE + "EchoStringResponse")]
        Task<string> EchoStringAsync(string echo);
    }

        [OperationContract]
        string ComplexEcho(EchoMessage text);

        [OperationContract]
        [FaultContract(typeof(EchoFault))]
        string FailEcho(string text);
    }

    [DataContract]
    public class EchoMessage
    {
        [DataMember]
        public string Text { get; set; }
    }
}
