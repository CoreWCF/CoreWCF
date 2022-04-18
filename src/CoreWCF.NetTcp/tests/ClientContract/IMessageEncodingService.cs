// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace ClientContract
{
    [System.ServiceModel.ServiceContract]
    public interface IMessageEncodingService
    {
        [System.ServiceModel.OperationContract]
        byte[] EchoByteArray(byte[] bytes);
    }
}
