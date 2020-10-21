//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------
namespace ClientContract
{
    using System.IO;
    using System.ServiceModel;

    [ServiceContract(CallbackContract = typeof(ClientContract.IPushCallback))]
    public interface IDuplexService
    {
        // Request-Reply operations
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

        // Duplex operations
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

        // Logging
        [OperationContract(IsOneWay = true)]
        void GetLog();
    }
}