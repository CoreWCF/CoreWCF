using System.IO;
using CoreWCF;
using CoreWCF.Channels;

namespace Services.AsyncStreamingService
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
