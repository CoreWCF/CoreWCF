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
}