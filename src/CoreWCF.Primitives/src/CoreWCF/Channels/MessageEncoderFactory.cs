// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public abstract class MessageEncoderFactory
    {
        protected MessageEncoderFactory()
        {
        }

        public abstract MessageEncoder Encoder
        {
            get;
        }

        public abstract MessageVersion MessageVersion
        {
            get;
        }

        public virtual MessageEncoder CreateSessionEncoder()
        {
            return Encoder;
        }
    }

    internal static class MessageEncodingPolicyConstants
    {
        public const string BinaryEncodingName = "BinaryEncoding";
        public const string BinaryEncodingNamespace = "http://schemas.microsoft.com/ws/06/2004/mspolicy/netbinary1";
        public const string BinaryEncodingPrefix = "msb";
        public const string OptimizedMimeSerializationNamespace = "http://schemas.xmlsoap.org/ws/2004/09/policy/optimizedmimeserialization";
        public const string OptimizedMimeSerializationPrefix = "wsoma";
        public const string MtomEncodingName = "OptimizedMimeSerialization";
    }
}
