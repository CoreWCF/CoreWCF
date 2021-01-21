// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using DictionaryManager = CoreWCF.IdentityModel.DictionaryManager;
using ISignatureValueSecurityElement = CoreWCF.IdentityModel.ISignatureValueSecurityElement;

namespace CoreWCF.Security
{
    internal class SignatureConfirmationElement : ISignatureValueSecurityElement
    {
        private readonly SecurityVersion version;
        private readonly string id;
        private readonly byte[] signatureValue;

        public SignatureConfirmationElement(string id, byte[] signatureValue, SecurityVersion version)
        {
            if (id == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            }
            if (signatureValue == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(signatureValue));
            }
            this.id = id;
            this.signatureValue = signatureValue;
            this.version = version;
        }

        public bool HasId => true;

        public string Id => id;

        public byte[] GetSignatureValue()
        {
            return signatureValue;
        }

        public void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            version.WriteSignatureConfirmation(writer, id, signatureValue);
        }
    }
}
