// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security
{
    public enum MessageProtectionOrder
    {
        SignBeforeEncrypt,
        SignBeforeEncryptAndEncryptSignature,
        EncryptBeforeSign,
    }

    internal static class MessageProtectionOrderHelper
    {
        internal static bool IsDefined(MessageProtectionOrder value)
        {
            return value == MessageProtectionOrder.SignBeforeEncrypt
                || value == MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature
                || value == MessageProtectionOrder.EncryptBeforeSign;
        }
    }
}
