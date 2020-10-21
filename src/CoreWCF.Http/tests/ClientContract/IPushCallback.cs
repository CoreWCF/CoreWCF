//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------
namespace ClientContract
{
    using System.Collections.Generic;
    using System.IO;
    using System.ServiceModel;
    using Xunit.Abstractions;

    public interface IPushCallback
    {
        [OperationContract(IsOneWay = true)]
        void ReceiveData(string data);

        [OperationContract(IsOneWay = true)]
        void ReceiveStream(Stream stream);

        [OperationContract(IsOneWay = true)]
        void ReceiveLog(List<string> log);

        [OperationContract(IsOneWay = true)]
        void ReceiveStreamWithException(Stream stream);
    }
}