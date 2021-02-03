// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;
using TokenEntry = CoreWCF.Security.WSSecurityTokenSerializer.TokenEntry;

namespace CoreWCF.Security
{
    internal class WSSecureConversationDec2005 : WSSecureConversation
    {
        private readonly SecurityStateEncoder _securityStateEncoder;
        private readonly IList<Type> _knownClaimTypes;

        public WSSecureConversationDec2005(WSSecurityTokenSerializer tokenSerializer, SecurityStateEncoder securityStateEncoder, IEnumerable<Type> knownTypes,
            int maxKeyDerivationOffset, int maxKeyDerivationLabelLength, int maxKeyDerivationNonceLength)
            : base(tokenSerializer, maxKeyDerivationOffset, maxKeyDerivationLabelLength, maxKeyDerivationNonceLength)
        {
            if (securityStateEncoder != null)
            {
                _securityStateEncoder = securityStateEncoder;
            }
            else
            {
                _securityStateEncoder = new DataProtectionSecurityStateEncoder();
            }

            _knownClaimTypes = new List<Type>();
            if (knownTypes != null)
            {
                // Clone this collection.
                foreach (Type knownType in knownTypes)
                {
                    _knownClaimTypes.Add(knownType);
                }
            }
        }

        public override SecureConversationDictionary SerializerDictionary
        {
            get { return DXD.SecureConversationDec2005Dictionary; }
        }

        public override void PopulateTokenEntries(IList<TokenEntry> tokenEntryList)
        {
            base.PopulateTokenEntries(tokenEntryList);
            tokenEntryList.Add(new SecurityContextTokenEntryDec2005(this, _securityStateEncoder, _knownClaimTypes));
        }

        public override string DerivationAlgorithm
        {
            get
            {
                return SecurityAlgorithms.Psha1KeyDerivationDec2005;
            }
        }

        private class SecurityContextTokenEntryDec2005 : SecurityContextTokenEntry
        {
            public SecurityContextTokenEntryDec2005(WSSecureConversationDec2005 parent, SecurityStateEncoder securityStateEncoder, IList<Type> knownClaimTypes)
                : base(parent, securityStateEncoder, knownClaimTypes)
            {
            }

            protected override bool CanReadGeneration(XmlDictionaryReader reader)
            {
                return reader.IsStartElement(DXD.SecureConversationDec2005Dictionary.Instance, DXD.SecureConversationDec2005Dictionary.Namespace);
            }

            protected override bool CanReadGeneration(XmlElement element)
            {
                return (element.LocalName == DXD.SecureConversationDec2005Dictionary.Instance.Value &&
                    element.NamespaceURI == DXD.SecureConversationDec2005Dictionary.Namespace.Value);
            }

            protected override UniqueId ReadGeneration(XmlDictionaryReader reader)
            {
                return reader.ReadElementContentAsUniqueId();
            }

            protected override UniqueId ReadGeneration(XmlElement element)
            {
                return XmlHelper.ReadTextElementAsUniqueId(element);
            }

            protected override void WriteGeneration(XmlDictionaryWriter writer, SecurityContextSecurityToken sct)
            {
                // serialize the generation
                if (sct.KeyGeneration != null)
                {
                    writer.WriteStartElement(DXD.SecureConversationDec2005Dictionary.Prefix.Value,
                        DXD.SecureConversationDec2005Dictionary.Instance,
                        DXD.SecureConversationDec2005Dictionary.Namespace);
                    XmlHelper.WriteStringAsUniqueId(writer, sct.KeyGeneration);
                    writer.WriteEndElement();
                }
            }
        }

        public class DriverDec2005 : Driver
        {
            public DriverDec2005()
            {
            }

            protected override SecureConversationDictionary DriverDictionary
            {
                get { return DXD.SecureConversationDec2005Dictionary; }
            }

            public override XmlDictionaryString CloseAction
            {
                get { return DXD.SecureConversationDec2005Dictionary.RequestSecurityContextClose; }
            }

            public override XmlDictionaryString CloseResponseAction
            {
                get { return DXD.SecureConversationDec2005Dictionary.RequestSecurityContextCloseResponse; }
            }

            public override bool IsSessionSupported
            {
                get { return true; }
            }

            public override XmlDictionaryString RenewAction
            {
                get { return DXD.SecureConversationDec2005Dictionary.RequestSecurityContextRenew; }
            }

            public override XmlDictionaryString RenewResponseAction
            {
                get { return DXD.SecureConversationDec2005Dictionary.RequestSecurityContextRenewResponse; }
            }

            public override XmlDictionaryString Namespace
            {
                get { return DXD.SecureConversationDec2005Dictionary.Namespace; }
            }

            public override string TokenTypeUri
            {
                get { return DXD.SecureConversationDec2005Dictionary.SecurityContextTokenType.Value; }
            }
        }
    }
}

