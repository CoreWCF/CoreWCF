using System.IO;
using System.ServiceModel;

namespace ClientContract
{    
    [ServiceContract(CallbackContract = typeof(ClientContract.IPushCallback))]
    public interface IDuplexService
    {
        [OperationContract]
        string GetExceptionString();

        [OperationContract]
        void UploadData(string data);

        [OperationContract]
        string DownloadData();

        [OperationContract(IsOneWay = true)]
        void UploadStream(Stream stream);

        [OperationContract]
        Stream DownloadStream();

        [OperationContract(IsOneWay = true)]
        void StartPushingData();

        [OperationContract(IsOneWay = true)]
        void StopPushingData();

        [OperationContract(IsOneWay = true)]
        void StartPushingStream();

        [OperationContract(IsOneWay = true)]
        void StartPushingStreamLongWait();

        [OperationContract(IsOneWay = true)]
        void StopPushingStream();

        [OperationContract(IsOneWay = true)]
        void GetLog();
    }
}
