using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract]
    public interface IRequestReplyService
    {
        [OperationContract]
        void UploadData(string data);

        [OperationContract]
        string DownloadData();

        [OperationContract]
        void UploadStream(Stream stream);

        [OperationContract]
        Stream DownloadStream();

        [OperationContract]
        Stream DownloadCustomizedStream(TimeSpan readThrottle, TimeSpan streamDuration);

        [OperationContract]
        void ThrowingOperation(Exception exceptionToThrow);

        [OperationContract]
        string DelayOperation(TimeSpan delay);

        [OperationContract]
        List<string> GetLog();
    }
}
