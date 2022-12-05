// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    public class MsmqPoisonMessageException : CommunicationException
    {
        public long MessageLookupId { get; }

        public MsmqPoisonMessageException(long messageLookupId, Exception innerException)
            : base(SR.MsmqPoisonMessage, innerException)
        {
            MessageLookupId = messageLookupId;
        }
    }
}
