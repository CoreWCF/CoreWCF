// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Services
{
    public class MessageEncodingService : ServiceContract.IMessageEncodingService
    {
        public byte[] EchoByteArray(byte[] bytes) => bytes;
    }
}
