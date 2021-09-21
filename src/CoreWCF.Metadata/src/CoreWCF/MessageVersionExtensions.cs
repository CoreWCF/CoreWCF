// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    internal static class MessageVersionExtensions
    {
        internal static bool IsMatch(this MessageVersion thisMessageVersion, MessageVersion messageVersion)
        {
            if (messageVersion == null)
            {
                Fx.Assert("Invalid (null) messageVersion value");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }
            if (thisMessageVersion.Addressing == null)
            {
                Fx.Assert("Invalid (null) addressing value");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "MessageVersion.Addressing cannot be null")));
            }

            if (thisMessageVersion.Envelope != messageVersion.Envelope)
            {
                return false;
            }

            if (thisMessageVersion.Addressing.Namespace != messageVersion.Addressing.Namespace)
            {
                return false;
            }

            return true;
        }
    }
}
