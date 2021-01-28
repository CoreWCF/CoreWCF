// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using DictionaryManager = CoreWCF.IdentityModel.DictionaryManager;
using ISignatureValueSecurityElement = CoreWCF.IdentityModel.ISignatureValueSecurityElement;

namespace CoreWCF.Security
{
    internal class SignatureConfirmationElement : ISignatureValueSecurityElement
    {
        private readonly SecurityVersion _version;
        private readonly byte[] _signatureValue;

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
            Id = id;
            _signatureValue = signatureValue;
            _version = version;
        }

        public bool HasId => true;

        public string Id { get; }

        public byte[] GetSignatureValue()
        {
            return _signatureValue;
        }

        public void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            _version.WriteSignatureConfirmation(writer, Id, _signatureValue);
        }
    }
}
