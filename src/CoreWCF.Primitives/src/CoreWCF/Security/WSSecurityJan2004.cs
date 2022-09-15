// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;
using HexBinary = CoreWCF.Security.SoapHexBinary;
using TokenEntry = CoreWCF.Security.WSSecurityTokenSerializer.TokenEntry;

namespace CoreWCF.Security
{
    internal class WSSecurityJan2004 : WSSecurityTokenSerializer.SerializerEntries
    {
        public WSSecurityJan2004(WSSecurityTokenSerializer tokenSerializer, SamlSerializer samlSerializer)
        {
            WSSecurityTokenSerializer = tokenSerializer;
            SamlSerializer = samlSerializer;
        }

        public WSSecurityTokenSerializer WSSecurityTokenSerializer { get; }

        public SamlSerializer SamlSerializer { get; }

        protected void PopulateJan2004TokenEntries(IList<TokenEntry> tokenEntryList)
        {
            tokenEntryList.Add(new GenericXmlTokenEntry());
            tokenEntryList.Add(new UserNamePasswordTokenEntry(WSSecurityTokenSerializer));
            tokenEntryList.Add(new X509TokenEntry(WSSecurityTokenSerializer));
        }

        public override void PopulateTokenEntries(IList<TokenEntry> tokenEntryList)
        {
            PopulateJan2004TokenEntries(tokenEntryList);
            tokenEntryList.Add(new SamlTokenEntry(WSSecurityTokenSerializer, SamlSerializer));
            tokenEntryList.Add(new WrappedKeyTokenEntry(WSSecurityTokenSerializer));
        }

        protected class SamlTokenEntry : TokenEntry
        {
            private readonly SamlSerializer _samlSerializer;
            private readonly SecurityTokenSerializer _tokenSerializer;

            public SamlTokenEntry(SecurityTokenSerializer tokenSerializer, SamlSerializer samlSerializer)
            {
                _tokenSerializer = tokenSerializer;
                if (samlSerializer != null)
                {
                    _samlSerializer = samlSerializer;
                }
                else
                {
                    _samlSerializer = new SamlSerializer();
                }
            }

            protected override XmlDictionaryString LocalName { get { return XD.SecurityJan2004Dictionary.SamlAssertion; } }
            protected override XmlDictionaryString NamespaceUri { get { return XD.SecurityJan2004Dictionary.SamlUri; } }
            protected override Type[] GetTokenTypesCore() { return new Type[] { typeof(SamlSecurityToken) }; }
            public override string TokenTypeUri { get { return null; } }
            protected override string ValueTypeUri { get { return null; } }

            //we don't need it, as we we populate differently in SamlSerializer
            public override SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromTokenXmlCore(XmlElement issuedTokenXml,
                SecurityTokenReferenceStyle tokenReferenceStyle)
            {
                TokenReferenceStyleHelper.Validate(tokenReferenceStyle);

                switch (tokenReferenceStyle)
                {
                    // SAML uses same reference for internal and external
                    case SecurityTokenReferenceStyle.Internal:
                    case SecurityTokenReferenceStyle.External:
                        throw new NotImplementedException();
                       // return new SamlAssertionKeyIdentifierClause(assertionId);
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(tokenReferenceStyle)));
                }
            }

            public override SecurityToken ReadTokenCore(XmlDictionaryReader reader, SecurityTokenResolver tokenResolver)
            {
                SamlSecurityToken samlToken = _samlSerializer.ReadToken(reader, _tokenSerializer, tokenResolver);
                return samlToken;
            }

            public override void WriteTokenCore(XmlDictionaryWriter writer, SecurityToken token)
            {
                throw new NotImplementedException();
            }
        }

        internal abstract class BinaryTokenEntry : TokenEntry
        {
            internal static readonly XmlDictionaryString s_elementName = XD.SecurityJan2004Dictionary.BinarySecurityToken;
            internal static readonly XmlDictionaryString s_encodingTypeAttribute = XD.SecurityJan2004Dictionary.EncodingType;
            internal const string EncodingTypeAttributeString = SecurityJan2004Strings.EncodingType;
            internal const string EncodingTypeValueBase64Binary = SecurityJan2004Strings.EncodingTypeValueBase64Binary;
            internal const string EncodingTypeValueHexBinary = SecurityJan2004Strings.EncodingTypeValueHexBinary;
            internal static readonly XmlDictionaryString s_valueTypeAttribute = XD.SecurityJan2004Dictionary.ValueType;

            private readonly WSSecurityTokenSerializer _tokenSerializer;
            private readonly string[] _valueTypeUris = null;

            protected BinaryTokenEntry(WSSecurityTokenSerializer tokenSerializer, string valueTypeUri)
            {
                _tokenSerializer = tokenSerializer;
                _valueTypeUris = new string[1];
                _valueTypeUris[0] = valueTypeUri;
            }

            protected BinaryTokenEntry(WSSecurityTokenSerializer tokenSerializer, string[] valueTypeUris)
            {
                if (valueTypeUris == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(valueTypeUris));
                }

                _tokenSerializer = tokenSerializer;
                _valueTypeUris = new string[valueTypeUris.GetLength(0)];
                for (int i = 0; i < _valueTypeUris.GetLength(0); ++i)
                {
                    _valueTypeUris[i] = valueTypeUris[i];
                }
            }

            protected override XmlDictionaryString LocalName { get { return s_elementName; } }
            protected override XmlDictionaryString NamespaceUri { get { return XD.SecurityJan2004Dictionary.Namespace; } }
            public override string TokenTypeUri { get { return _valueTypeUris[0]; } }
            protected override string ValueTypeUri { get { return _valueTypeUris[0]; } }
            public override bool SupportsTokenTypeUri(string tokenTypeUri)
            {
                for (int i = 0; i < _valueTypeUris.GetLength(0); ++i)
                {
                    if (_valueTypeUris[i] == tokenTypeUri)
                    {
                        return true;
                    }
                }

                return false;
            }

            public abstract SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromBinaryCore(byte[] rawData);

            public override SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromTokenXmlCore(XmlElement issuedTokenXml,
                SecurityTokenReferenceStyle tokenReferenceStyle)
            {
                TokenReferenceStyleHelper.Validate(tokenReferenceStyle);

                switch (tokenReferenceStyle)
                {
                    case SecurityTokenReferenceStyle.Internal:
                        return CreateDirectReference(issuedTokenXml, UtilityStrings.IdAttribute, UtilityStrings.Namespace, TokenType);
                    case SecurityTokenReferenceStyle.External:
                        string encoding = issuedTokenXml.GetAttribute(EncodingTypeAttributeString, null);
                        string encodedData = issuedTokenXml.InnerText;

                        byte[] binaryData;
                        if (encoding == null || encoding == EncodingTypeValueBase64Binary)
                        {
                            binaryData = Convert.FromBase64String(encodedData);
                        }
                        else if (encoding == EncodingTypeValueHexBinary)
                        {
                            binaryData = HexBinary.Parse(encodedData).Value;
                        }
                        else
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.UnknownEncodingInBinarySecurityToken));
                        }

                        return CreateKeyIdentifierClauseFromBinaryCore(binaryData);
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(tokenReferenceStyle)));
                }
            }

            public abstract SecurityToken ReadBinaryCore(string id, string valueTypeUri, byte[] rawData);

            public override SecurityToken ReadTokenCore(XmlDictionaryReader reader, SecurityTokenResolver tokenResolver)
            {
                string wsuId = reader.GetAttribute(XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace);
                string valueTypeUri = reader.GetAttribute(s_valueTypeAttribute, null);
                string encoding = reader.GetAttribute(s_encodingTypeAttribute, null);

                byte[] binaryData;
                if (encoding == null || encoding == EncodingTypeValueBase64Binary)
                {
                    binaryData = reader.ReadElementContentAsBase64();
                }
                else if (encoding == EncodingTypeValueHexBinary)
                {
                    binaryData = HexBinary.Parse(reader.ReadElementContentAsString()).Value;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.UnknownEncodingInBinarySecurityToken));
                }

                return ReadBinaryCore(wsuId, valueTypeUri, binaryData);
            }

            public abstract void WriteBinaryCore(SecurityToken token, out string id, out byte[] rawData);

            public override void WriteTokenCore(XmlDictionaryWriter writer, SecurityToken token)
            {
                WriteBinaryCore(token, out string id, out byte[] rawData);

                if (rawData == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rawData));
                }

                writer.WriteStartElement(XD.SecurityJan2004Dictionary.Prefix.Value, s_elementName, XD.SecurityJan2004Dictionary.Namespace);
                if (id != null)
                {
                    writer.WriteAttributeString(XD.UtilityDictionary.Prefix.Value, XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace, id);
                }
                if (_valueTypeUris != null)
                {
                    writer.WriteAttributeString(s_valueTypeAttribute, null, _valueTypeUris[0]);
                }
                if (_tokenSerializer.EmitBspRequiredAttributes)
                {
                    writer.WriteAttributeString(s_encodingTypeAttribute, null, EncodingTypeValueBase64Binary);
                }
                writer.WriteBase64(rawData, 0, rawData.Length);
                writer.WriteEndElement(); // BinarySecurityToken
            }
        }

        private class GenericXmlTokenEntry : TokenEntry
        {
            protected override XmlDictionaryString LocalName { get { return null; } }
            protected override XmlDictionaryString NamespaceUri { get { return null; } }
            protected override Type[] GetTokenTypesCore() { return new Type[] { typeof(GenericXmlSecurityToken) }; }
            public override string TokenTypeUri { get { return null; } }
            protected override string ValueTypeUri { get { return null; } }

            public GenericXmlTokenEntry()
            {
            }


            public override bool CanReadTokenCore(XmlElement element)
            {
                return false;
            }

            public override bool CanReadTokenCore(XmlDictionaryReader reader)
            {
                return false;
            }

            public override SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromTokenXmlCore(XmlElement issuedTokenXml,
                SecurityTokenReferenceStyle tokenReferenceStyle)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }

            public override SecurityToken ReadTokenCore(XmlDictionaryReader reader, SecurityTokenResolver tokenResolver)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
            }

            public override void WriteTokenCore(XmlDictionaryWriter writer, SecurityToken token)
            {
                if (token is BufferedGenericXmlSecurityToken bufferedXmlToken && bufferedXmlToken.TokenXmlBuffer != null)
                {
                    using (XmlDictionaryReader reader = bufferedXmlToken.TokenXmlBuffer.GetReader(0))
                    {
                        writer.WriteNode(reader, false);
                    }
                }
                else
                {
                    GenericXmlSecurityToken xmlToken = (GenericXmlSecurityToken)token;
                    xmlToken.TokenXml.WriteTo(writer);
                }
            }
        }

        protected class WrappedKeyTokenEntry : TokenEntry
        {
            private readonly WSSecurityTokenSerializer _tokenSerializer;

            public WrappedKeyTokenEntry(WSSecurityTokenSerializer tokenSerializer)
            {
                _tokenSerializer = tokenSerializer;
            }

            protected override XmlDictionaryString LocalName { get { return EncryptedKey.ElementName; } }
            protected override XmlDictionaryString NamespaceUri { get { return XD.XmlEncryptionDictionary.Namespace; } }
            protected override Type[] GetTokenTypesCore() { return new Type[] { typeof(WrappedKeySecurityToken) }; }
            public override string TokenTypeUri { get { return null; } }
            protected override string ValueTypeUri { get { return null; } }

            public override SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromTokenXmlCore(XmlElement issuedTokenXml,
                SecurityTokenReferenceStyle tokenReferenceStyle)
            {

                TokenReferenceStyleHelper.Validate(tokenReferenceStyle);

                switch (tokenReferenceStyle)
                {
                    case SecurityTokenReferenceStyle.Internal:
                        return CreateDirectReference(issuedTokenXml, XmlEncryptionStrings.Id, null, null);
                    case SecurityTokenReferenceStyle.External:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.CantInferReferenceForToken, EncryptedKey.ElementName.Value)));
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(tokenReferenceStyle)));
                }
            }

            public override SecurityToken ReadTokenCore(XmlDictionaryReader reader, SecurityTokenResolver tokenResolver)
            {
                EncryptedKey encryptedKey = new EncryptedKey
                {
                    SecurityTokenSerializer = _tokenSerializer
                };
                encryptedKey.ReadFrom(reader);
                SecurityKeyIdentifier unwrappingTokenIdentifier = encryptedKey.KeyIdentifier;
                byte[] wrappedKey = encryptedKey.GetWrappedKey();
                WrappedKeySecurityToken wrappedKeyToken = CreateWrappedKeyToken(encryptedKey.Id, encryptedKey.EncryptionMethod,
                    encryptedKey.CarriedKeyName, unwrappingTokenIdentifier, wrappedKey, tokenResolver);
                wrappedKeyToken.EncryptedKey = encryptedKey;

                return wrappedKeyToken;
            }

            private WrappedKeySecurityToken CreateWrappedKeyToken(string id, string encryptionMethod, string carriedKeyName,
                SecurityKeyIdentifier unwrappingTokenIdentifier, byte[] wrappedKey, SecurityTokenResolver tokenResolver)
            {
                if (tokenResolver is ISspiNegotiationInfo sspiResolver)
                {
                    ISspiNegotiation unwrappingSspiContext = sspiResolver.SspiNegotiation;
                    // ensure that the encryption algorithm is compatible
                    if (encryptionMethod != unwrappingSspiContext.KeyEncryptionAlgorithm)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.BadKeyEncryptionAlgorithm, encryptionMethod)));
                    }
                    byte[] unwrappedKey = unwrappingSspiContext.Decrypt(wrappedKey);
                    return new WrappedKeySecurityToken(id, unwrappedKey, encryptionMethod, unwrappingSspiContext, unwrappedKey);
                }
                else
                {
                    if (tokenResolver == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(tokenResolver)));
                    }
                    if (unwrappingTokenIdentifier == null || unwrappingTokenIdentifier.Count == 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.MissingKeyInfoInEncryptedKey)));
                    }

                    SecurityToken unwrappingToken;
                    if (tokenResolver is SecurityHeaderTokenResolver resolver)
                    {
                        unwrappingToken = resolver.ExpectedWrapper;
                        if (unwrappingToken != null)
                        {
                            if (!resolver.CheckExternalWrapperMatch(unwrappingTokenIdentifier))
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                                    SR.Format(SR.EncryptedKeyWasNotEncryptedWithTheRequiredEncryptingToken, unwrappingToken)));
                            }
                        }
                        else
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                                SR.Format(SR.UnableToResolveKeyInfoForUnwrappingToken, unwrappingTokenIdentifier, resolver)));
                        }
                    }
                    else
                    {
                        try
                        {
                            unwrappingToken = tokenResolver.ResolveToken(unwrappingTokenIdentifier);
                        }
                        catch (Exception exception)
                        {
                            if (exception is MessageSecurityException)
                                throw;

                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(
                                SR.Format(SR.UnableToResolveKeyInfoForUnwrappingToken, unwrappingTokenIdentifier, tokenResolver), exception));
                        }
                    }
                    byte[] unwrappedKey = SecurityUtils.DecryptKey(unwrappingToken, encryptionMethod, wrappedKey, out SecurityKey unwrappingSecurityKey);
                    return new WrappedKeySecurityToken(id, unwrappedKey, encryptionMethod, unwrappingToken, unwrappingTokenIdentifier, wrappedKey, unwrappingSecurityKey);
                }
            }

            public override void WriteTokenCore(XmlDictionaryWriter writer, SecurityToken token)
            {
                WrappedKeySecurityToken wrappedKeyToken = token as WrappedKeySecurityToken;
                wrappedKeyToken.EnsureEncryptedKeySetUp();
                wrappedKeyToken.EncryptedKey.SecurityTokenSerializer = _tokenSerializer;
                wrappedKeyToken.EncryptedKey.WriteTo(writer, ServiceModelDictionaryManager.Instance);
            }
        }

        private class UserNamePasswordTokenEntry : TokenEntry
        {
            private readonly WSSecurityTokenSerializer _tokenSerializer;

            public UserNamePasswordTokenEntry(WSSecurityTokenSerializer tokenSerializer)
            {
                _tokenSerializer = tokenSerializer;
            }

            protected override XmlDictionaryString LocalName { get { return XD.SecurityJan2004Dictionary.UserNameTokenElement; } }
            protected override XmlDictionaryString NamespaceUri { get { return XD.SecurityJan2004Dictionary.Namespace; } }
            protected override Type[] GetTokenTypesCore() { return new Type[] { typeof(UserNameSecurityToken) }; }
            public override string TokenTypeUri { get { return SecurityJan2004Strings.UPTokenType; } }
            protected override string ValueTypeUri { get { return null; } }

            public override SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromTokenXmlCore(XmlElement issuedTokenXml,
                SecurityTokenReferenceStyle tokenReferenceStyle)
            {
                TokenReferenceStyleHelper.Validate(tokenReferenceStyle);

                switch (tokenReferenceStyle)
                {
                    case SecurityTokenReferenceStyle.Internal:
                        return CreateDirectReference(issuedTokenXml, UtilityStrings.IdAttribute, UtilityStrings.Namespace, typeof(UserNameSecurityToken));
                    case SecurityTokenReferenceStyle.External:
                        // UP tokens aren't referred to externally
                        return null;
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(tokenReferenceStyle)));
                }
            }

            public override SecurityToken ReadTokenCore(XmlDictionaryReader reader, SecurityTokenResolver tokenResolver)
            {
                ParseToken(reader, out string id, out string userName, out string password);

                if (id == null)
                {
                    id = SecurityUniqueId.Create().Value;
                }

                return new UserNameSecurityToken(userName, password, id);
            }

            public override void WriteTokenCore(XmlDictionaryWriter writer, SecurityToken token)
            {
                UserNameSecurityToken upToken = (UserNameSecurityToken)token;
                WriteUserNamePassword(writer, upToken.Id, upToken.UserName, upToken.Password);
            }

            private void WriteUserNamePassword(XmlDictionaryWriter writer, string id, string userName, string password)
            {
                writer.WriteStartElement(XD.SecurityJan2004Dictionary.Prefix.Value, XD.SecurityJan2004Dictionary.UserNameTokenElement,
                    XD.SecurityJan2004Dictionary.Namespace); // <wsse:UsernameToken
                writer.WriteAttributeString(XD.UtilityDictionary.Prefix.Value, XD.UtilityDictionary.IdAttribute,
                    XD.UtilityDictionary.Namespace, id); // wsu:Id="..."
                writer.WriteElementString(XD.SecurityJan2004Dictionary.Prefix.Value, XD.SecurityJan2004Dictionary.UserNameElement,
                    XD.SecurityJan2004Dictionary.Namespace, userName); // ><wsse:Username>...</wsse:Username>
                if (password != null)
                {
                    writer.WriteStartElement(XD.SecurityJan2004Dictionary.Prefix.Value, XD.SecurityJan2004Dictionary.PasswordElement,
                        XD.SecurityJan2004Dictionary.Namespace);
                    if (_tokenSerializer.EmitBspRequiredAttributes)
                    {
                        writer.WriteAttributeString(XD.SecurityJan2004Dictionary.TypeAttribute, null, SecurityJan2004Strings.UPTokenPasswordTextValue);
                    }
                    writer.WriteString(password); // <wsse:Password>...</wsse:Password>
                    writer.WriteEndElement();
                }
                writer.WriteEndElement(); // </wsse:UsernameToken>
            }

            private static string ParsePassword(XmlDictionaryReader reader)
            {
                string type = reader.GetAttribute(XD.SecurityJan2004Dictionary.TypeAttribute, null);
                if (type != null && type.Length > 0 && type != SecurityJan2004Strings.UPTokenPasswordTextValue)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedPasswordType, type)));
                }

                return reader.ReadElementString();
            }

            private static void ParseToken(XmlDictionaryReader reader, out string id, out string userName, out string password)
            {
                userName = null;
                password = null;

                reader.MoveToContent();
                id = reader.GetAttribute(XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace);

                reader.ReadStartElement(XD.SecurityJan2004Dictionary.UserNameTokenElement, XD.SecurityJan2004Dictionary.Namespace);
                while (reader.IsStartElement())
                {
                    if (reader.IsStartElement(XD.SecurityJan2004Dictionary.UserNameElement, XD.SecurityJan2004Dictionary.Namespace))
                    {
                        userName = reader.ReadElementString();
                    }
                    else if (reader.IsStartElement(XD.SecurityJan2004Dictionary.PasswordElement, XD.SecurityJan2004Dictionary.Namespace))
                    {
                        password = ParsePassword(reader);
                    }
                    else if (reader.IsStartElement(XD.SecurityJan2004Dictionary.NonceElement, XD.SecurityJan2004Dictionary.Namespace))
                    {
                        // Nonce can be safely ignored
                        reader.Skip();
                    }
                    else if (reader.IsStartElement(XD.UtilityDictionary.CreatedElement, XD.UtilityDictionary.Namespace))
                    {
                        // wsu:Created can be safely ignored
                        reader.Skip();
                    }
                    else
                    {
                        throw new NotImplementedException();
                        //  XmlHelper.OnUnexpectedChildNodeError(SecurityJan2004Strings.UserNameTokenElement, reader);
                    }
                }
                reader.ReadEndElement();

                if (userName == null)
                {
                    throw new NotImplementedException();
                    //  XmlHelper.OnRequiredElementMissing(SecurityJan2004Strings.UserNameElement, SecurityJan2004Strings.Namespace);
                }
            }
        }

        protected class X509TokenEntry : BinaryTokenEntry
        {
            internal const string ValueTypeAbsoluteUri = SecurityJan2004Strings.X509TokenType;

            public X509TokenEntry(WSSecurityTokenSerializer tokenSerializer)
                : base(tokenSerializer, ValueTypeAbsoluteUri)
            {
            }

            protected override Type[] GetTokenTypesCore() { return new Type[] { typeof(X509SecurityToken) }; }

            public override SecurityKeyIdentifierClause CreateKeyIdentifierClauseFromBinaryCore(byte[] rawData)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.CantInferReferenceForToken, ValueTypeAbsoluteUri)));
            }

            public override SecurityToken ReadBinaryCore(string id, string valueTypeUri, byte[] rawData)
            {
                if (!SecurityUtils.TryCreateX509CertificateFromRawData(rawData, out X509Certificate2 certificate))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.InvalidX509RawData));
                }
                return new X509SecurityToken(certificate, id, false);
            }

            public override void WriteBinaryCore(SecurityToken token, out string id, out byte[] rawData)
            {
                id = token.Id;
                if (token is X509SecurityToken x509Token)
                {
                    rawData = x509Token.Certificate.GetRawCertData();
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }
            }
        }

        public class IdManager : SignatureTargetIdManager
        {
            private IdManager()
            {
            }

            public override string DefaultIdNamespacePrefix
            {
                get { return UtilityStrings.Prefix; }
            }

            public override string DefaultIdNamespaceUri
            {
                get { return UtilityStrings.Namespace; }
            }

            internal static IdManager Instance { get; } = new IdManager();

            public override string ExtractId(XmlDictionaryReader reader)
            {
                if (reader == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
                }

                return reader.GetAttribute(XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace);
            }

            public override void WriteIdAttribute(XmlDictionaryWriter writer, string id)
            {
                if (writer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
                }

                writer.WriteAttributeString(XD.UtilityDictionary.Prefix.Value, XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace, id);
            }
        }
    }
}
