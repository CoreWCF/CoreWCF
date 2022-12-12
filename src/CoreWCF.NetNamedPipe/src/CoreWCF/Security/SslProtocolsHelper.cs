// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Security.Authentication;

namespace CoreWCF.Security
{
    internal static class SslProtocolsHelper
    {
        internal static bool IsDefined(SslProtocols value)
        {
            SslProtocols allValues = SslProtocols.None;
            foreach (object protocol in Enum.GetValues(typeof(SslProtocols)))
            {
                allValues |= (SslProtocols)protocol;
            }
            return (value & allValues) == value;
        }

        internal static void Validate(SslProtocols value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(SslProtocols)));
            }
        }
    }
}
