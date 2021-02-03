// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    [DataContract]
    internal class MatchNoneMessageFilter : MessageFilter
    {
        public MatchNoneMessageFilter()
            : base()
        {
        }

        public override bool Match(MessageBuffer buffer)
        {
            if (buffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));
            }
            return false;
        }

        public override bool Match(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }
            return false;
        }
    }
}