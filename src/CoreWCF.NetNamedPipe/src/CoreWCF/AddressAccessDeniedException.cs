// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF
{
    [Serializable]
    public class AddressAccessDeniedException : CommunicationException
    {
        public AddressAccessDeniedException() { }
        public AddressAccessDeniedException(string message) : base(message) { }
        public AddressAccessDeniedException(string message, Exception innerException) : base(message, innerException) { }
        protected AddressAccessDeniedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
