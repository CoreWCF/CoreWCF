// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Contract
{
    [ServiceContract]
    internal interface IStreamService
    {
        [OperationContract]
        Task<long> SendStreamAsync(Stream requestStream);

        [OperationContract]
        Task<Stream> GetStreamAsync(long streamSize);
    }
}
