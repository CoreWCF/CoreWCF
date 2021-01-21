// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using CoreWCF;

namespace ServiceContract
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
