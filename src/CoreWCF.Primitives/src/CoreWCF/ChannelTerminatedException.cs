// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    //[Serializable]
    internal class ChannelTerminatedException : CommunicationException
    {
        public ChannelTerminatedException() { }
        public ChannelTerminatedException(string message) : base(message) { }
        public ChannelTerminatedException(string message, Exception innerException) : base(message, innerException) { }
        //protected ChannelTerminatedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}