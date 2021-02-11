// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    //[Serializable]
    public class EndpointNotFoundException : CommunicationException
    {
        public EndpointNotFoundException() { }
        public EndpointNotFoundException(string message) : base(message) { }
        public EndpointNotFoundException(string message, Exception innerException) : base(message, innerException) { }
        //protected EndpointNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}