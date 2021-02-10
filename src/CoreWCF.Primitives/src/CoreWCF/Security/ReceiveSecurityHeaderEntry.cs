// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security
{
    internal struct ReceiveSecurityHeaderEntry
    {
        internal ReceiveSecurityHeaderElementCategory elementCategory;
        internal object element;
        internal ReceiveSecurityHeaderBindingModes bindingMode;
        internal string id;
        internal string encryptedFormId;
        internal string encryptedFormWsuId;
        internal bool signed;
        internal bool encrypted;
        internal byte[] decryptedBuffer;
        internal TokenTracker supportingTokenTracker;
        internal bool doubleEncrypted;

        public bool MatchesId(string id, bool requiresEncryptedFormId)
        {
            if (doubleEncrypted)
            {
                return (encryptedFormId == id || encryptedFormWsuId == id);
            }
            else
            {
                if (requiresEncryptedFormId)
                {
                    return encryptedFormId == id;
                }
                else
                {
                    return this.id == id;
                }
            }
        }

        public void PreserveIdBeforeDecryption()
        {
            encryptedFormId = id;
        }

        public void SetElement(
            ReceiveSecurityHeaderElementCategory elementCategory, object element,
            ReceiveSecurityHeaderBindingModes bindingMode, string id, bool encrypted, byte[] decryptedBuffer, TokenTracker supportingTokenTracker)
        {
            this.elementCategory = elementCategory;
            this.element = element;
            this.bindingMode = bindingMode;
            this.encrypted = encrypted;
            this.decryptedBuffer = decryptedBuffer;
            this.supportingTokenTracker = supportingTokenTracker;
            this.id = id;
        }
    }
}
