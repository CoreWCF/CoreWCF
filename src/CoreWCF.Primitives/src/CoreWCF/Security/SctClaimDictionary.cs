// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace CoreWCF.Security
{
    internal sealed class SctClaimDictionary : XmlDictionary
    {
        private readonly XmlDictionaryString emptyString;

        private SctClaimDictionary()
        {
            SecurityContextSecurityToken = Add("SecurityContextSecurityToken");
            Version = Add("Version");
            ContextId = Add("ContextId");
            Id = Add("Id");
            Key = Add("Key");
            IsCookieMode = Add("IsCookieMode");
            ServiceContractId = Add("ServiceContractId");
            EffectiveTime = Add("EffectiveTime");
            ExpiryTime = Add("ExpiryTime");
            KeyGeneration = Add("KeyGeneration");
            KeyEffectiveTime = Add("KeyEffectiveTime");
            KeyExpiryTime = Add("KeyExpiryTime");
            Claim = Add("Claim");
            ClaimSets = Add("ClaimSets");
            ClaimSet = Add("ClaimSet");
            Identities = Add("Identities");
            PrimaryIdentity = Add("PrimaryIdentity");
            PrimaryIssuer = Add("PrimaryIssuer");

            X509CertificateClaimSet = Add("X509CertificateClaimSet");
            SystemClaimSet = Add("SystemClaimSet");
            WindowsClaimSet = Add("WindowsClaimSet");
            AnonymousClaimSet = Add("AnonymousClaimSet");

            BinaryClaim = Add("BinaryClaim");
            DnsClaim = Add("DnsClaim");
            GenericIdentity = Add("GenericIdentity");
            AuthenticationType = Add("AuthenticationType");
            Right = Add("Right");
            HashClaim = Add("HashClaim");
            MailAddressClaim = Add("MailAddressClaim");
            NameClaim = Add("NameClaim");
            RsaClaim = Add("RsaClaim");
            SpnClaim = Add("SpnClaim");
            SystemClaim = Add("SystemClaim");
            UpnClaim = Add("UpnClaim");
            UrlClaim = Add("UrlClaim");
            WindowsSidClaim = Add("WindowsSidClaim");
            DenyOnlySidClaim = Add("DenyOnlySidClaim");
            WindowsSidIdentity = Add("WindowsSidIdentity");
            X500DistinguishedNameClaim = Add("X500DistinguishedClaim");
            X509ThumbprintClaim = Add("X509ThumbprintClaim");

            Name = Add("Name");
            Sid = Add("Sid");
            Value = Add("Value");
            NullValue = Add("Null");
            GenericXmlSecurityToken = Add("GenericXmlSecurityToken");
            TokenType = Add("TokenType");
            InternalTokenReference = Add("InternalTokenReference");
            ExternalTokenReference = Add("ExternalTokenReference");
            TokenXml = Add("TokenXml");
            emptyString = Add(String.Empty);
        }

        public static SctClaimDictionary Instance { get; } = new SctClaimDictionary();

        public XmlDictionaryString Claim { get; }

        public XmlDictionaryString ClaimSets { get; }

        public XmlDictionaryString ClaimSet { get; }

        public XmlDictionaryString PrimaryIssuer { get; }

        public XmlDictionaryString Identities { get; }

        public XmlDictionaryString PrimaryIdentity { get; }

        public XmlDictionaryString X509CertificateClaimSet { get; }

        public XmlDictionaryString SystemClaimSet { get; }

        public XmlDictionaryString WindowsClaimSet { get; }

        public XmlDictionaryString AnonymousClaimSet { get; }

        public XmlDictionaryString ContextId { get; }

        public XmlDictionaryString BinaryClaim { get; }

        public XmlDictionaryString DnsClaim { get; }

        public XmlDictionaryString GenericIdentity { get; }

        public XmlDictionaryString AuthenticationType { get; }

        public XmlDictionaryString Right { get; }

        public XmlDictionaryString HashClaim { get; }

        public XmlDictionaryString MailAddressClaim { get; }

        public XmlDictionaryString NameClaim { get; }

        public XmlDictionaryString RsaClaim { get; }

        public XmlDictionaryString SpnClaim { get; }

        public XmlDictionaryString SystemClaim { get; }

        public XmlDictionaryString UpnClaim { get; }

        public XmlDictionaryString UrlClaim { get; }

        public XmlDictionaryString WindowsSidClaim { get; }

        public XmlDictionaryString DenyOnlySidClaim { get; }

        public XmlDictionaryString WindowsSidIdentity { get; }

        public XmlDictionaryString X500DistinguishedNameClaim { get; }

        public XmlDictionaryString X509ThumbprintClaim { get; }

        public XmlDictionaryString EffectiveTime { get; }

        public XmlDictionaryString ExpiryTime { get; }

        public XmlDictionaryString Id { get; }

        public XmlDictionaryString IsCookieMode { get; }

        public XmlDictionaryString Key { get; }

        public XmlDictionaryString Sid { get; }

        public XmlDictionaryString Name { get; }

        public XmlDictionaryString NullValue { get; }

        public XmlDictionaryString SecurityContextSecurityToken { get; }

        public XmlDictionaryString ServiceContractId { get; }

        public XmlDictionaryString Value { get; }

        public XmlDictionaryString Version { get; }

        public XmlDictionaryString GenericXmlSecurityToken { get; }

        public XmlDictionaryString TokenType { get; }

        public XmlDictionaryString TokenXml { get; }

        public XmlDictionaryString InternalTokenReference { get; }

        public XmlDictionaryString ExternalTokenReference { get; }

        public XmlDictionaryString EmptyString => emptyString;

        public XmlDictionaryString KeyGeneration { get; }

        public XmlDictionaryString KeyEffectiveTime { get; }

        public XmlDictionaryString KeyExpiryTime { get; }
    }
}
