// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class WSKeyInfoSerializer : KeyInfoSerializer
    {
        private static Func<KeyInfoSerializer, IEnumerable<SecurityTokenSerializer.SerializerEntries>> CreateAdditionalEntries(SecurityVersion securityVersion, SecureConversationVersion secureConversationVersion)
        {
            return (KeyInfoSerializer keyInfoSerializer) =>
            {
                List<SecurityTokenSerializer.SerializerEntries> serializerEntries = new List<SecurityTokenSerializer.SerializerEntries>();

                if (securityVersion == SecurityVersion.WSSecurity10)
                {
                    serializerEntries.Add(new CoreWCF.IdentityModel.Tokens.WSSecurityJan2004(keyInfoSerializer));
                }
                else if (securityVersion == SecurityVersion.WSSecurity11)
                {
                    serializerEntries.Add(new CoreWCF.IdentityModel.Tokens.WSSecurityXXX2005(keyInfoSerializer));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("securityVersion", SR.Format(SR.MessageSecurityVersionOutOfRange)));
                }

                if (secureConversationVersion == SecureConversationVersion.WSSecureConversationFeb2005)
                {
                    serializerEntries.Add(new WSSecureConversationFeb2005(keyInfoSerializer));
                }
                else if (secureConversationVersion == SecureConversationVersion.WSSecureConversation13)
                {
                    serializerEntries.Add(new WSSecureConversationDec2005(keyInfoSerializer));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                }

                return serializerEntries;
            };
        }

        public WSKeyInfoSerializer(bool emitBspRequiredAttributes, DictionaryManager dictionaryManager, TrustDictionary trustDictionary, SecurityTokenSerializer innerSecurityTokenSerializer, SecurityVersion securityVersion, SecureConversationVersion secureConversationVersion)
            : base(emitBspRequiredAttributes, dictionaryManager, trustDictionary, innerSecurityTokenSerializer, CreateAdditionalEntries(securityVersion, secureConversationVersion))
        {
        }

        #region WSSecureConversation classes

        public abstract class WSSecureConversation : SecurityTokenSerializer.SerializerEntries
        {
            protected WSSecureConversation(KeyInfoSerializer securityTokenSerializer)
            {
                SecurityTokenSerializer = securityTokenSerializer;
            }

            public KeyInfoSerializer SecurityTokenSerializer { get; }

            public abstract SecureConversationDictionary SerializerDictionary { get; }

            public virtual string DerivationAlgorithm => SecurityAlgorithms.Psha1KeyDerivation;

            public override void PopulateTokenEntries(IList<TokenEntry> tokenEntryList)
            {
                if (tokenEntryList == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("tokenEntryList");
                }
                tokenEntryList.Add(new DerivedKeyTokenEntry(this));
                tokenEntryList.Add(new SecurityContextTokenEntry(this));
            }

            protected abstract class SctStrEntry : StrEntry
            {
                public SctStrEntry(WSSecureConversation parent)
                {
                    Parent = parent;
                }

                protected WSSecureConversation Parent { get; }

                public override Type GetTokenType(SecurityKeyIdentifierClause clause)
                {
                    return null;
                }

                public override string GetTokenTypeUri()
                {
                    return null;
                }

                public override bool CanReadClause(XmlDictionaryReader reader, string tokenType)
                {
                    if (tokenType != null && tokenType != Parent.SerializerDictionary.SecurityContextTokenType.Value)
                    {
                        return false;
                    }
                    if (reader.IsStartElement(
                        Parent.SecurityTokenSerializer.DictionaryManager.SecurityJan2004Dictionary.Reference,
                        Parent.SecurityTokenSerializer.DictionaryManager.SecurityJan2004Dictionary.Namespace))
                    {
                        string valueType = reader.GetAttribute(Parent.SecurityTokenSerializer.DictionaryManager.SecurityJan2004Dictionary.ValueType, null);
                        if (valueType != null && valueType != Parent.SerializerDictionary.SecurityContextTokenReferenceValueType.Value)
                        {
                            return false;
                        }
                        string uri = reader.GetAttribute(Parent.SecurityTokenSerializer.DictionaryManager.SecurityJan2004Dictionary.URI, null);
                        if (uri != null)
                        {
                            if (uri.Length > 0 && uri[0] != '#')
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }

                public override SecurityKeyIdentifierClause ReadClause(XmlDictionaryReader reader, byte[] derivationNonce, int derivationLength, string tokenType)
                {
                    System.Xml.UniqueId uri = XmlHelper.GetAttributeAsUniqueId(reader, XD.SecurityJan2004Dictionary.URI, null);
                    System.Xml.UniqueId generation = ReadGeneration(reader);

                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                    }
                    else
                    {
                        reader.ReadStartElement();
                        while (reader.IsStartElement())
                        {
                            reader.Skip();
                        }
                        reader.ReadEndElement();
                    }

                    return new SecurityContextKeyIdentifierClause(uri, generation, derivationNonce, derivationLength);
                }

                protected abstract System.Xml.UniqueId ReadGeneration(XmlDictionaryReader reader);

                public override bool SupportsCore(SecurityKeyIdentifierClause clause)
                {
                    return clause is SecurityContextKeyIdentifierClause;
                }

                public override void WriteContent(XmlDictionaryWriter writer, SecurityKeyIdentifierClause clause)
                {
                    SecurityContextKeyIdentifierClause sctClause = clause as SecurityContextKeyIdentifierClause;
                    writer.WriteStartElement(XD.SecurityJan2004Dictionary.Prefix.Value, XD.SecurityJan2004Dictionary.Reference, XD.SecurityJan2004Dictionary.Namespace);
                    XmlHelper.WriteAttributeStringAsUniqueId(writer, null, XD.SecurityJan2004Dictionary.URI, null, sctClause.ContextId);
                    WriteGeneration(writer, sctClause);
                    writer.WriteAttributeString(XD.SecurityJan2004Dictionary.ValueType, null, Parent.SerializerDictionary.SecurityContextTokenReferenceValueType.Value);
                    writer.WriteEndElement();
                }

                protected abstract void WriteGeneration(XmlDictionaryWriter writer, SecurityContextKeyIdentifierClause clause);
            }

            protected class SecurityContextTokenEntry : SecurityTokenSerializer.TokenEntry
            {
                private Type[] tokenTypes;

                public SecurityContextTokenEntry(WSSecureConversation parent)
                {
                    Parent = parent;
                }

                protected WSSecureConversation Parent { get; }

                protected override XmlDictionaryString LocalName => Parent.SerializerDictionary.SecurityContextToken;
                protected override XmlDictionaryString NamespaceUri => Parent.SerializerDictionary.Namespace;
                protected override Type[] GetTokenTypesCore()
                {
                    if (tokenTypes == null)
                    {
                        tokenTypes = new Type[] { typeof(SecurityContextSecurityToken) };
                    }

                    return tokenTypes;
                }
                public override string TokenTypeUri => Parent.SerializerDictionary.SecurityContextTokenType.Value;
                protected override string ValueTypeUri => null;

            }

            protected class DerivedKeyTokenEntry : SecurityTokenSerializer.TokenEntry
            {
                public const string DefaultLabel = "WS-SecureConversation";
                private readonly WSSecureConversation parent;
                private Type[] tokenTypes;

                public DerivedKeyTokenEntry(WSSecureConversation parent)
                {
                    if (parent == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("parent");
                    }
                    this.parent = parent;
                }

                protected override XmlDictionaryString LocalName => parent.SerializerDictionary.DerivedKeyToken;
                protected override XmlDictionaryString NamespaceUri => parent.SerializerDictionary.Namespace;
                protected override Type[] GetTokenTypesCore()
                {
                    if (tokenTypes == null)
                    {
                        tokenTypes = new Type[] { typeof(DerivedKeySecurityToken) };
                    }

                    return tokenTypes;
                }

                public override string TokenTypeUri => parent.SerializerDictionary.DerivedKeyTokenType.Value;
                protected override string ValueTypeUri => null;
            }
        }

        private class WSSecureConversationFeb2005 : WSSecureConversation
        {
            public WSSecureConversationFeb2005(KeyInfoSerializer securityTokenSerializer)
                : base(securityTokenSerializer)
            {
            }

            public override SecureConversationDictionary SerializerDictionary => SecurityTokenSerializer.DictionaryManager.SecureConversationFeb2005Dictionary;

            public override void PopulateStrEntries(IList<StrEntry> strEntries)
            {
                strEntries.Add(new SctStrEntryFeb2005(this));
            }

            private class SctStrEntryFeb2005 : SctStrEntry
            {
                public SctStrEntryFeb2005(WSSecureConversationFeb2005 parent)
                    : base(parent)
                {
                }

                protected override System.Xml.UniqueId ReadGeneration(XmlDictionaryReader reader)
                {
                    return XmlHelper.GetAttributeAsUniqueId(
                        reader,
                        Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationDec2005Dictionary.Instance,
                        Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationFeb2005Dictionary.Namespace);
                }

                protected override void WriteGeneration(XmlDictionaryWriter writer, SecurityContextKeyIdentifierClause clause)
                {
                    // serialize the generation
                    if (clause.Generation != null)
                    {
                        XmlHelper.WriteAttributeStringAsUniqueId(
                            writer,
                            Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationFeb2005Dictionary.Prefix.Value,
                            Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationDec2005Dictionary.Instance,
                            Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationFeb2005Dictionary.Namespace,
                            clause.Generation);
                    }
                }
            }
        }

        private class WSSecureConversationDec2005 : WSSecureConversation
        {
            public WSSecureConversationDec2005(KeyInfoSerializer securityTokenSerializer) : base(securityTokenSerializer)
            {
            }

            public override SecureConversationDictionary SerializerDictionary => SecurityTokenSerializer.DictionaryManager.SecureConversationDec2005Dictionary;

            public override void PopulateStrEntries(IList<StrEntry> strEntries)
            {
                strEntries.Add(new SctStrEntryDec2005(this));
            }

            public override string DerivationAlgorithm => SecurityAlgorithms.Psha1KeyDerivationDec2005;

            private class SctStrEntryDec2005 : SctStrEntry
            {
                public SctStrEntryDec2005(WSSecureConversationDec2005 parent)
                    : base(parent)
                {
                }

                protected override System.Xml.UniqueId ReadGeneration(XmlDictionaryReader reader)
                {
                    return XmlHelper.GetAttributeAsUniqueId(reader, Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationDec2005Dictionary.Instance,
                        Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationDec2005Dictionary.Namespace);
                }

                protected override void WriteGeneration(XmlDictionaryWriter writer, SecurityContextKeyIdentifierClause clause)
                {
                    // serialize the generation
                    if (clause.Generation != null)
                    {
                        XmlHelper.WriteAttributeStringAsUniqueId(
                            writer,
                            Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationDec2005Dictionary.Prefix.Value,
                            Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationDec2005Dictionary.Instance,
                            Parent.SecurityTokenSerializer.DictionaryManager.SecureConversationDec2005Dictionary.Namespace,
                            clause.Generation);
                    }
                }
            }

        }

        #endregion
    }
}
