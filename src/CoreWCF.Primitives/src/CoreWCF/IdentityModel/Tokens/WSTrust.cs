// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel.Security
{
    using System;
    using System.Collections.Generic;
    using System.Xml;
    using CoreWCF.IdentityModel;
    using CoreWCF.IdentityModel.Selectors;
    using CoreWCF.IdentityModel.Tokens;
    using CoreWCF.Security;
    using CoreWCF.Security.Tokens;
    using KeyIdentifierClauseEntry = CoreWCF.IdentityModel.Selectors.SecurityTokenSerializer.KeyIdentifierClauseEntry;

    internal class WSTrust : SecurityTokenSerializer.SerializerEntries
    {
        private readonly KeyInfoSerializer _securityTokenSerializer;

        public WSTrust(KeyInfoSerializer securityTokenSerializer, TrustDictionary serializerDictionary)
        {
            _securityTokenSerializer = securityTokenSerializer;
            SerializerDictionary = serializerDictionary;
        }

        public TrustDictionary SerializerDictionary { get; }

        public override void PopulateTokenEntries(IList<SecurityTokenSerializer.TokenEntry> tokenEntryList)
        {
            tokenEntryList.Add(new BinarySecretTokenEntry(this));
        }

        public override void PopulateKeyIdentifierClauseEntries(IList<SecurityTokenSerializer.KeyIdentifierClauseEntry> keyIdentifierClauseEntries)
        {
            keyIdentifierClauseEntries.Add(new BinarySecretClauseEntry(this));
            keyIdentifierClauseEntries.Add(new GenericXmlSecurityKeyIdentifierClauseEntry(this));
        }

        private class BinarySecretTokenEntry : SecurityTokenSerializer.TokenEntry
        {
            private readonly WSTrust _parent;

            public BinarySecretTokenEntry(WSTrust parent)
            {
                _parent = parent;
            }

            protected override XmlDictionaryString LocalName { get { return _parent.SerializerDictionary.BinarySecret; } }
            protected override XmlDictionaryString NamespaceUri { get { return _parent.SerializerDictionary.Namespace; } }
            protected override Type[] GetTokenTypesCore() { return new Type[] { typeof(BinarySecretSecurityToken) }; }
            public override string TokenTypeUri { get { return null; } }
            protected override string ValueTypeUri { get { return null; } }
        }

        internal class BinarySecretClauseEntry : KeyIdentifierClauseEntry
        {
            private readonly WSTrust _parent;
            private readonly TrustDictionary _otherDictionary = null;

            public BinarySecretClauseEntry(WSTrust parent)
            {
                _parent = parent;

                _otherDictionary = null;

                if (parent.SerializerDictionary is TrustDec2005Dictionary)
                {
                    _otherDictionary = parent._securityTokenSerializer.DictionaryManager.TrustFeb2005Dictionary;
                }

                if (parent.SerializerDictionary is TrustFeb2005Dictionary)
                {
                    _otherDictionary = parent._securityTokenSerializer.DictionaryManager.TrustDec2005Dictionary;
                }

                // always set it, so we don't have to worry about null
                if (_otherDictionary == null)
                {
                    _otherDictionary = _parent.SerializerDictionary;
                }
            }

            protected override XmlDictionaryString LocalName
            {
                get { return _parent.SerializerDictionary.BinarySecret; }
            }

            protected override XmlDictionaryString NamespaceUri
            {
                get { return _parent.SerializerDictionary.Namespace; }
            }

            public override SecurityKeyIdentifierClause ReadKeyIdentifierClauseCore(XmlDictionaryReader reader)
            {
                byte[] secret = reader.ReadElementContentAsBase64();
                return new BinarySecretKeyIdentifierClause(secret, false);
            }

            public override bool SupportsCore(SecurityKeyIdentifierClause keyIdentifierClause)
            {
                return keyIdentifierClause is BinarySecretKeyIdentifierClause;
            }

            public override bool CanReadKeyIdentifierClauseCore(XmlDictionaryReader reader)
            {
                return (reader.IsStartElement(LocalName, NamespaceUri) || reader.IsStartElement(LocalName, _otherDictionary.Namespace));
            }

            public override void WriteKeyIdentifierClauseCore(XmlDictionaryWriter writer, SecurityKeyIdentifierClause keyIdentifierClause)
            {
                BinarySecretKeyIdentifierClause skic = keyIdentifierClause as BinarySecretKeyIdentifierClause;
                byte[] secret = skic.GetKeyBytes();
                writer.WriteStartElement(_parent.SerializerDictionary.Prefix.Value, _parent.SerializerDictionary.BinarySecret, _parent.SerializerDictionary.Namespace);
                writer.WriteBase64(secret, 0, secret.Length);
                writer.WriteEndElement();
            }
        }

        internal class GenericXmlSecurityKeyIdentifierClauseEntry : KeyIdentifierClauseEntry
        {
            private readonly WSTrust _parent;

            public GenericXmlSecurityKeyIdentifierClauseEntry(WSTrust parent)
            {
                _parent = parent;
            }

            protected override XmlDictionaryString LocalName
            {
                get { return null; }
            }

            protected override XmlDictionaryString NamespaceUri
            {
                get { return null; }
            }

            public override bool CanReadKeyIdentifierClauseCore(XmlDictionaryReader reader)
            {
                return false;
            }

            public override SecurityKeyIdentifierClause ReadKeyIdentifierClauseCore(XmlDictionaryReader reader)
            {
                return null;
            }

            public override bool SupportsCore(SecurityKeyIdentifierClause keyIdentifierClause)
            {
                return keyIdentifierClause is GenericXmlSecurityKeyIdentifierClause;
            }

            public override void WriteKeyIdentifierClauseCore(XmlDictionaryWriter writer, SecurityKeyIdentifierClause keyIdentifierClause)
            {
                GenericXmlSecurityKeyIdentifierClause genericXmlSecurityKeyIdentifierClause = keyIdentifierClause as GenericXmlSecurityKeyIdentifierClause;
                genericXmlSecurityKeyIdentifierClause.ReferenceXml.WriteTo(writer);
            }
        }

        protected static bool CheckElement(XmlElement element, string name, string ns, out string value)
        {
            value = null;
            if (element.LocalName != name || element.NamespaceURI != ns)
            {
                return false;
            }

            if (element.FirstChild is XmlText)
            {
                value = ((XmlText)element.FirstChild).Value;
                return true;
            }
            return false;
        }
    }
}
