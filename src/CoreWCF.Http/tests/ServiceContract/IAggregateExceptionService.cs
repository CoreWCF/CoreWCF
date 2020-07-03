using CoreWCF;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace ServiceContract
{
    [DataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    public class SampleServiceFault
    {
        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string ID { get; set; }
    }

    [ServiceContract]
    public interface IAggregateExceptionService
    {
        [OperationContract]
        [FaultContract(typeof(SampleServiceFault))]
        Task SimpleOperationThrowingFault();

        [OperationContract]
        [FaultContract(typeof(SampleServiceFault))]
        void ServiceOpWithMultipleTasks();

        [OperationContract]
        [FaultContract(typeof(SampleServiceFault))]
        Task ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTask();
    }
}
