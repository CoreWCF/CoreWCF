// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace CoreWCF.Security
{
    sealed class SctClaimDictionary : XmlDictionary
    {
        XmlDictionaryString x509CertificateClaimSet;
        XmlDictionaryString binaryClaim;
        XmlDictionaryString x509ThumbprintClaim;
        XmlDictionaryString windowsSidIdentity;
        XmlDictionaryString contextId;
        XmlDictionaryString name;
        XmlDictionaryString genericXmlToken;
        XmlDictionaryString emptyString;

        private SctClaimDictionary()
        {
            this.SecurityContextSecurityToken = this.Add("SecurityContextSecurityToken");
            this.Version = this.Add("Version");
            this.contextId = this.Add("ContextId");
            this.Id = this.Add("Id");
            this.Key = this.Add("Key");
            this.IsCookieMode = this.Add("IsCookieMode");
            this.ServiceContractId = this.Add("ServiceContractId");
            this.EffectiveTime = this.Add("EffectiveTime");
            this.ExpiryTime = this.Add("ExpiryTime");
            this.KeyGeneration = this.Add("KeyGeneration");
            this.KeyEffectiveTime = this.Add("KeyEffectiveTime");
            this.KeyExpiryTime = this.Add("KeyExpiryTime");
            this.Claim = this.Add("Claim");
            this.ClaimSets = this.Add("ClaimSets");
            this.ClaimSet = this.Add("ClaimSet");
            this.Identities = this.Add("Identities");
            this.PrimaryIdentity = this.Add("PrimaryIdentity");
            this.PrimaryIssuer = this.Add("PrimaryIssuer");

            this.x509CertificateClaimSet = this.Add("X509CertificateClaimSet");
            this.SystemClaimSet = this.Add("SystemClaimSet");
            this.WindowsClaimSet = this.Add("WindowsClaimSet");
            this.AnonymousClaimSet = this.Add("AnonymousClaimSet");

            this.binaryClaim = this.Add("BinaryClaim");
            this.DnsClaim = this.Add("DnsClaim");
            this.GenericIdentity = this.Add("GenericIdentity");
            this.AuthenticationType = this.Add("AuthenticationType");
            this.Right = this.Add("Right");
            this.HashClaim = this.Add("HashClaim");
            this.MailAddressClaim = this.Add("MailAddressClaim");
            this.NameClaim = this.Add("NameClaim");
            this.RsaClaim = this.Add("RsaClaim");
            this.SpnClaim = this.Add("SpnClaim");
            this.SystemClaim = this.Add("SystemClaim");
            this.UpnClaim = this.Add("UpnClaim");
            this.UrlClaim = this.Add("UrlClaim");
            this.WindowsSidClaim = this.Add("WindowsSidClaim");
            this.DenyOnlySidClaim = this.Add("DenyOnlySidClaim");
            this.windowsSidIdentity = this.Add("WindowsSidIdentity");
            this.X500DistinguishedNameClaim = this.Add("X500DistinguishedClaim");
            this.x509ThumbprintClaim = this.Add("X509ThumbprintClaim");

            this.name = this.Add("Name");
            this.Sid = this.Add("Sid");
            this.Value = this.Add("Value");
            this.NullValue = this.Add("Null");
            this.genericXmlToken = this.Add("GenericXmlSecurityToken");
            this.TokenType = this.Add("TokenType");
            this.InternalTokenReference = this.Add("InternalTokenReference");
            this.ExternalTokenReference = this.Add("ExternalTokenReference");
            this.TokenXml = this.Add("TokenXml");
            this.emptyString = this.Add(String.Empty);
        }

        public static SctClaimDictionary Instance { get; } = new SctClaimDictionary();

        public XmlDictionaryString Claim { get; }

        public XmlDictionaryString ClaimSets { get; }

        public XmlDictionaryString ClaimSet { get; }

        public XmlDictionaryString PrimaryIssuer { get; }

        public XmlDictionaryString Identities { get; }

        public XmlDictionaryString PrimaryIdentity { get; }

        public XmlDictionaryString X509CertificateClaimSet => this.x509CertificateClaimSet;

        public XmlDictionaryString SystemClaimSet { get; }

        public XmlDictionaryString WindowsClaimSet { get; }

        public XmlDictionaryString AnonymousClaimSet { get; }

        public XmlDictionaryString ContextId => this.contextId;

        public XmlDictionaryString BinaryClaim => this.binaryClaim;

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

        public XmlDictionaryString WindowsSidIdentity => this.windowsSidIdentity;

        public XmlDictionaryString X500DistinguishedNameClaim { get; }

        public XmlDictionaryString X509ThumbprintClaim => this.x509ThumbprintClaim;

        public XmlDictionaryString EffectiveTime { get; }

        public XmlDictionaryString ExpiryTime { get; }

        public XmlDictionaryString Id { get; }

        public XmlDictionaryString IsCookieMode { get; }

        public XmlDictionaryString Key { get; }

        public XmlDictionaryString Sid { get; }

        public XmlDictionaryString Name => this.name;

        public XmlDictionaryString NullValue { get; }

        public XmlDictionaryString SecurityContextSecurityToken { get; }

        public XmlDictionaryString ServiceContractId { get; }

        public XmlDictionaryString Value { get; }

        public XmlDictionaryString Version { get; }

        public XmlDictionaryString GenericXmlSecurityToken => this.genericXmlToken;

        public XmlDictionaryString TokenType { get; }

        public XmlDictionaryString TokenXml { get; }

        public XmlDictionaryString InternalTokenReference { get; }

        public XmlDictionaryString ExternalTokenReference { get; }

        public XmlDictionaryString EmptyString => this.emptyString;

        public XmlDictionaryString KeyGeneration { get; }

        public XmlDictionaryString KeyEffectiveTime { get; }

        public XmlDictionaryString KeyExpiryTime { get; }
    }
}
