// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum MessageCredentialType
    {
        None,
        Windows,
        UserName,
        Certificate,
        IssuedToken
    }

    internal static class MessageCredentialTypeHelper
    {
        public static bool IsDefined(MessageCredentialType value)
        {
            return (value == MessageCredentialType.None ||
                value == MessageCredentialType.UserName ||
                value == MessageCredentialType.Windows ||
                value == MessageCredentialType.Certificate ||
                value == MessageCredentialType.IssuedToken);
        }
    }
}
