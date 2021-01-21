// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF
{
    [Serializable]
    public class ServerTooBusyException : CommunicationException
    {
        public ServerTooBusyException() { }
        public ServerTooBusyException(string message) : base(message) { }
        public ServerTooBusyException(string message, Exception innerException) : base(message, innerException) { }
        protected ServerTooBusyException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}