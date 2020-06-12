using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace ClientContract.AsyncStreamingService
{
    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        Stream GetStream();

        [OperationContract]
        Message GetMessage();

        [OperationContract]
        Stream GetSlowStream();

        [OperationContract]
        void CloseStream();
    }
}
