using System.Threading.Tasks;

namespace Contract
{
    [CoreWCF.ServiceContract]
    [System.ServiceModel.ServiceContract]
    public interface ITaskService
    {
        [CoreWCF.OperationContract]
        [System.ServiceModel.OperationContract]
        Task SynchronousCompletion();

        [CoreWCF.OperationContract]
        [System.ServiceModel.OperationContract]
        Task AsynchronousCompletion();
    }
}
