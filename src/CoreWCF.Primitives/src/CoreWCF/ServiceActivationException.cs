// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF
{
    [Serializable]
    public class ServiceActivationException : CommunicationException
    {
        public ServiceActivationException() { }
        public ServiceActivationException(string message) : base(message) { }
        public ServiceActivationException(string message, Exception innerException) : base(message, innerException) { }
        protected ServiceActivationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
