﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.Security.Tokens;
using static CoreWCF.IdentityModel.Selectors.SecurityTokenSerializer;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class WSSecurityXXX2005 : WSSecurityJan2004
    {
        public WSSecurityXXX2005(KeyInfoSerializer securityTokenSerializer)
            : base(securityTokenSerializer)
        {
        }

        public override void PopulateStrEntries(IList<StrEntry> strEntries)
        {
            PopulateJan2004StrEntries(strEntries);
            //  strEntries.Add(new SamlDirectStrEntry());
            strEntries.Add(new X509ThumbprintStrEntry(SecurityTokenSerializer.EmitBspRequiredAttributes));
            strEntries.Add(new EncryptedKeyHashStrEntry(SecurityTokenSerializer.EmitBspRequiredAttributes));
        }

        public override void PopulateTokenEntries(IList<TokenEntry> tokenEntryList)
        {
            PopulateJan2004TokenEntries(tokenEntryList);
            tokenEntryList.Add(new WSSecurityXXX2005.WrappedKeyTokenEntry());
            // tokenEntryList.Add(new WSSecurityXXX2005.SamlTokenEntry());
        }

        public override void PopulateKeyIdentifierClauseEntries(IList<KeyIdentifierClauseEntry> clauseEntries)
        {
            List<StrEntry> strEntries = new List<StrEntry>();
            SecurityTokenSerializer.PopulateStrEntries(strEntries);
            SecurityTokenReferenceXXX2005ClauseEntry strClause = new SecurityTokenReferenceXXX2005ClauseEntry(SecurityTokenSerializer.EmitBspRequiredAttributes, strEntries);
            clauseEntries.Add(strClause);
        }

        private new class WrappedKeyTokenEntry : WSSecurityJan2004.WrappedKeyTokenEntry
        {
            public override string TokenTypeUri { get { return SecurityXXX2005Strings.EncryptedKeyTokenType; } }
        }

        private class SecurityTokenReferenceXXX2005ClauseEntry : SecurityTokenReferenceJan2004ClauseEntry
        {
            public SecurityTokenReferenceXXX2005ClauseEntry(bool emitBspRequiredAttributes, IList<StrEntry> strEntries)
                : base(emitBspRequiredAttributes, strEntries)
            {
            }

            protected override string ReadTokenType(XmlDictionaryReader reader)
            {
                return reader.GetAttribute(CoreWCF.XD.SecurityXXX2005Dictionary.TokenTypeAttribute, CoreWCF.XD.SecurityXXX2005Dictionary.Namespace);
            }

            public override void WriteKeyIdentifierClauseCore(XmlDictionaryWriter writer, SecurityKeyIdentifierClause keyIdentifierClause)
            {
                for (int i = 0; i < StrEntries.Count; ++i)
                {
                    if (StrEntries[i].SupportsCore(keyIdentifierClause))
                    {
                        writer.WriteStartElement(CoreWCF.XD.SecurityJan2004Dictionary.Prefix.Value, CoreWCF.XD.SecurityJan2004Dictionary.SecurityTokenReference, CoreWCF.XD.SecurityJan2004Dictionary.Namespace);

                        string tokenTypeUri = GetTokenTypeUri(StrEntries[i], keyIdentifierClause);
                        if (tokenTypeUri != null)
                        {
                            writer.WriteAttributeString(CoreWCF.XD.SecurityXXX2005Dictionary.Prefix.Value, CoreWCF.XD.SecurityXXX2005Dictionary.TokenTypeAttribute, CoreWCF.XD.SecurityXXX2005Dictionary.Namespace, tokenTypeUri);
                        }

                        StrEntries[i].WriteContent(writer, keyIdentifierClause);
                        writer.WriteEndElement();
                        return;
                    }
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.StandardsManagerCannotWriteObject, keyIdentifierClause.GetType())));
            }

            private string GetTokenTypeUri(StrEntry str, SecurityKeyIdentifierClause keyIdentifierClause)
            {
                bool emitTokenType = EmitTokenType(str);
                if (emitTokenType)
                {
                    string tokenTypeUri;
                    if (str is LocalReferenceStrEntry)
                    {
                        tokenTypeUri = (str as LocalReferenceStrEntry).GetLocalTokenTypeUri(keyIdentifierClause);
                        // only emit token type for SAML,Kerberos and Encrypted References
                        switch (tokenTypeUri)
                        {
                            case SecurityXXX2005Strings.Saml20TokenType:
                            case SecurityXXX2005Strings.SamlTokenType:
                            case SecurityXXX2005Strings.EncryptedKeyTokenType:
                            case SecurityJan2004Strings.KerberosTokenTypeGSS: break;

                            default:
                                tokenTypeUri = null;
                                break;
                        }
                    }
                    else
                    {
                        tokenTypeUri = str.GetTokenTypeUri();
                    }

                    return tokenTypeUri;
                }
                else
                {
                    return null;
                }
            }

            private bool EmitTokenType(StrEntry str)
            {
                bool emitTokenType = false;
                // we emit tokentype always for SAML and Encrypted Key Tokens 
                if (
                        //(str is SamlJan2004KeyIdentifierStrEntry)
                        //||
                        (str is EncryptedKeyHashStrEntry)
                       // || (str is SamlDirectStrEntry)
                       )
                {
                    emitTokenType = true;
                }
                else if (EmitBspRequiredAttributes)
                {
                    if (
                        //(str is KerberosHashStrEntry)
                        //||
                        (str is LocalReferenceStrEntry))
                    {
                        emitTokenType = true;
                    }
                }
                return emitTokenType;
            }
        }

        private class EncryptedKeyHashStrEntry : WSSecurityJan2004.KeyIdentifierStrEntry
        {
            protected override Type ClauseType { get { return typeof(EncryptedKeyHashIdentifierClause); } }
            public override Type TokenType { get { return typeof(WrappedKeySecurityToken); } }
            protected override string ValueTypeUri { get { return SecurityXXX2005Strings.EncryptedKeyHashValueType; } }

            public EncryptedKeyHashStrEntry(bool emitBspRequiredAttributes)
                : base(emitBspRequiredAttributes)
            {
            }

            public override bool CanReadClause(XmlDictionaryReader reader, string tokenType)
            {
                // Backward compatible with V1. Accept if missing.
                if (tokenType != null && tokenType != SecurityXXX2005Strings.EncryptedKeyTokenType)
                {
                    return false;
                }
                return base.CanReadClause(reader, tokenType);
            }

            protected override SecurityKeyIdentifierClause CreateClause(byte[] bytes, byte[] derivationNonce, int derivationLength)
            {
                return new EncryptedKeyHashIdentifierClause(bytes, true, derivationNonce, derivationLength);
            }

            public override string GetTokenTypeUri()
            {
                return SecurityXXX2005Strings.EncryptedKeyTokenType;
            }
        }

        private class X509ThumbprintStrEntry : WSSecurityJan2004.KeyIdentifierStrEntry
        {
            protected override Type ClauseType { get { return typeof(X509ThumbprintKeyIdentifierClause); } }
            public override Type TokenType { get { return typeof(X509SecurityToken); } }
            protected override string ValueTypeUri { get { return SecurityXXX2005Strings.ThumbprintSha1ValueType; } }

            public X509ThumbprintStrEntry(bool emitBspRequiredAttributes)
                : base(emitBspRequiredAttributes)
            {
            }

            protected override SecurityKeyIdentifierClause CreateClause(byte[] bytes, byte[] derivationNonce, int derivationLength)
            {
                return new X509ThumbprintKeyIdentifierClause(bytes);
            }
            public override string GetTokenTypeUri()
            {
                return CoreWCF.XD.SecurityXXX2005Dictionary.ThumbprintSha1ValueType.Value;
            }
        }

        //class SamlDirectStrEntry : StrEntry
        //{
        //    public override bool CanReadClause(XmlDictionaryReader reader, string tokenType)
        //    {
        //        if (tokenType != CoreWCF.XD.SecurityXXX2005Dictionary.Saml20TokenType.Value)
        //        {
        //            return false;
        //        }
        //        return (reader.IsStartElement(CoreWCF.XD.SecurityJan2004Dictionary.Reference, CoreWCF.XD.SecurityJan2004Dictionary.Namespace));
        //    }

        //    public override Type GetTokenType(SecurityKeyIdentifierClause clause)
        //    {
        //        return null;
        //    }

        //    public override string GetTokenTypeUri()
        //    {
        //        return CoreWCF.XD.SecurityXXX2005Dictionary.Saml20TokenType.Value;
        //    }

        //    public override SecurityKeyIdentifierClause ReadClause(XmlDictionaryReader reader, byte[] derivationNone, int derivationLength, string tokenType)
        //    {
        //        string samlUri = reader.GetAttribute(CoreWCF.XD.SecurityJan2004Dictionary.URI, null);
        //        if (reader.IsEmptyElement)
        //        {
        //            reader.Read();
        //        }
        //        else
        //        {
        //            reader.ReadStartElement();
        //            reader.ReadEndElement();
        //        }
        //        return new SamlAssertionDirectKeyIdentifierClause(samlUri, derivationNone, derivationLength);
        //    }

        //    public override bool SupportsCore(SecurityKeyIdentifierClause clause)
        //    {
        //        return typeof(SamlAssertionDirectKeyIdentifierClause).IsAssignableFrom(clause.GetType());
        //    }

        //    public override void WriteContent(XmlDictionaryWriter writer, SecurityKeyIdentifierClause clause)
        //    {
        //        SamlAssertionDirectKeyIdentifierClause samlClause = clause as SamlAssertionDirectKeyIdentifierClause;
        //        writer.WriteStartElement(CoreWCF.XD.SecurityJan2004Dictionary.Prefix.Value, CoreWCF.XD.SecurityJan2004Dictionary.Reference, CoreWCF.XD.SecurityJan2004Dictionary.Namespace);
        //        writer.WriteAttributeString(CoreWCF.XD.SecurityJan2004Dictionary.URI, null, samlClause.SamlUri);
        //        writer.WriteEndElement();
        //    }
        //}
    }
}