// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    public class CommunicationObjectAbortedException : CommunicationException
    {
        public CommunicationObjectAbortedException() { }
        public CommunicationObjectAbortedException(string message) : base(message) { }
        public CommunicationObjectAbortedException(string message, Exception innerException) : base(message, innerException) { }
        //protected CommunicationObjectAbortedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}