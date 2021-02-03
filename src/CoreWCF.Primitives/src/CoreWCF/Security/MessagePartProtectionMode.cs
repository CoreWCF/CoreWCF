// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security
{
    internal enum MessagePartProtectionMode
    {
        None,
        Sign,
        Encrypt,
        SignThenEncrypt,
        EncryptThenSign,
    }

    internal static class MessagePartProtectionModeHelper
    {
        public static MessagePartProtectionMode GetProtectionMode(bool sign, bool encrypt, bool signThenEncrypt)
        {
            if (sign)
            {
                if (encrypt)
                {
                    if (signThenEncrypt)
                    {
                        return MessagePartProtectionMode.SignThenEncrypt;
                    }
                    else
                    {
                        return MessagePartProtectionMode.EncryptThenSign;
                    }
                }
                else
                {
                    return MessagePartProtectionMode.Sign;
                }
            }
            else if (encrypt)
            {
                return MessagePartProtectionMode.Encrypt;
            }
            else
            {
                return MessagePartProtectionMode.None;
            }
        }
    }
}
