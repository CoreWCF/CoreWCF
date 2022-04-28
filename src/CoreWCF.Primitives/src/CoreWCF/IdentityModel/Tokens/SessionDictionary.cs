// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace CoreWCF.IdentityModel.Claims
{
    /// <summary>
    /// Dictionary of naming elements relevant to Windows Identity Foundation.
    /// </summary>
    internal sealed class SessionDictionary : XmlDictionary
    {
        private SessionDictionary()
        {
            Claim = Add("Claim");
            SecurityContextToken = Add("SecurityContextToken");
            Version = Add("Version");
            SecureConversationVersion = Add("SecureConversationVersion");
            Issuer = Add("Issuer");
            OriginalIssuer = Add("OriginalIssuer");
            IssuerRef = Add("IssuerRef");
            ClaimCollection = Add("ClaimCollection");
            Actor = Add("Actor");
            ClaimProperty = Add("ClaimProperty");
            ClaimProperties = Add("ClaimProperties");
            Value = Add("Value");
            ValueType = Add("ValueType");
            Label = Add("Label");
            Type = Add("Type");
            SubjectId = Add("subjectID");
            ClaimPropertyName = Add("ClaimPropertyName");
            ClaimPropertyValue = Add("ClaimPropertyValue");
            AuthenticationType = Add("AuthenticationType");
            NameClaimType = Add("NameClaimType");
            RoleClaimType = Add("RoleClaimType");
            NullValue = Add("Null");
            EmptyString = Add(String.Empty);
            Key = Add("Key");
            EffectiveTime = Add("EffectiveTime");
            ExpiryTime = Add("ExpiryTime");
            KeyGeneration = Add("KeyGeneration");
            KeyEffectiveTime = Add("KeyEffectiveTime");
            KeyExpiryTime = Add("KeyExpiryTime");
            SessionId = Add("SessionId");
            Id = Add("Id");
            ValidFrom = Add("ValidFrom");
            ValidTo = Add("ValidTo");
            ContextId = Add("ContextId");
            SessionToken = Add("SessionToken");
            SessionTokenCookie = Add("SessionTokenCookie");
            BootstrapToken = Add("BootStrapToken");
            Context = Add("Context");
            ClaimsPrincipal = Add("ClaimsPrincipal");
            WindowsPrincipal = Add("WindowsPrincipal");
            WindowsIdentity = Add("WindowIdentity");
            Identity = Add("Identity");
            Identities = Add("Identities");
            WindowsLogonName = Add("WindowsLogonName");
            PersistentTrue = Add("PersistentTrue");
            SctAuthorizationPolicy = Add("SctAuthorizationPolicy");
            Right = Add("Right");
            EndpointId = Add("EndpointId");
            WindowsSidClaim = Add("WindowsSidClaim");
            DenyOnlySidClaim = Add("DenyOnlySidClaim");
            X500DistinguishedNameClaim = Add("X500DistinguishedNameClaim");
            X509ThumbprintClaim = Add("X509ThumbprintClaim");
            NameClaim = Add("NameClaim");
            DnsClaim = Add("DnsClaim");
            RsaClaim = Add("RsaClaim");
            MailAddressClaim = Add("MailAddressClaim");
            SystemClaim = Add("SystemClaim");
            HashClaim = Add("HashClaim");
            SpnClaim = Add("SpnClaim");
            UpnClaim = Add("UpnClaim");
            UrlClaim = Add("UrlClaim");
            Sid = Add("Sid");
            ReferenceModeTrue = Add("ReferenceModeTrue");
        }

        public static SessionDictionary Instance { get; } = new SessionDictionary();

        public XmlDictionaryString PersistentTrue { get; }

        public XmlDictionaryString WindowsLogonName { get; }

        public XmlDictionaryString ClaimsPrincipal { get; }

        public XmlDictionaryString WindowsPrincipal { get; }

        public XmlDictionaryString WindowsIdentity { get; }

        public XmlDictionaryString Identity { get; }

        public XmlDictionaryString Identities { get; }

        public XmlDictionaryString SessionId { get; }

        public XmlDictionaryString ReferenceModeTrue { get; }

        public XmlDictionaryString ValidFrom { get; }

        public XmlDictionaryString ValidTo { get; }

        public XmlDictionaryString EffectiveTime { get; }

        public XmlDictionaryString ExpiryTime { get; }

        public XmlDictionaryString KeyEffectiveTime { get; }

        public XmlDictionaryString KeyExpiryTime { get; }

        public XmlDictionaryString Claim { get; }

        public XmlDictionaryString Issuer { get; }

        public XmlDictionaryString OriginalIssuer { get; }

        public XmlDictionaryString IssuerRef { get; }

        public XmlDictionaryString ClaimCollection { get; }

        public XmlDictionaryString Actor { get; }

        public XmlDictionaryString ClaimProperties { get; }

        public XmlDictionaryString ClaimProperty { get; }

        public XmlDictionaryString Value { get; }

        public XmlDictionaryString ValueType { get; }

        public XmlDictionaryString Label { get; }

        public XmlDictionaryString Type { get; }

        public XmlDictionaryString SubjectId { get; }

        public XmlDictionaryString ClaimPropertyName { get; }

        public XmlDictionaryString ClaimPropertyValue { get; }

        public XmlDictionaryString AuthenticationType { get; }

        public XmlDictionaryString NameClaimType { get; }

        public XmlDictionaryString RoleClaimType { get; }

        public XmlDictionaryString NullValue { get; }

        public XmlDictionaryString SecurityContextToken { get; }

        public XmlDictionaryString Version { get; }

        public XmlDictionaryString SecureConversationVersion { get; }

        public XmlDictionaryString EmptyString { get; }

        public XmlDictionaryString Key { get; }

        public XmlDictionaryString KeyGeneration { get; }

        public XmlDictionaryString Id { get; }

        public XmlDictionaryString ContextId { get; }

        public XmlDictionaryString SessionToken { get; }

        public XmlDictionaryString SessionTokenCookie { get; }

        public XmlDictionaryString BootstrapToken { get; }

        public XmlDictionaryString Context { get; }

        public XmlDictionaryString SctAuthorizationPolicy { get; }

        public XmlDictionaryString Right { get; }

        public XmlDictionaryString EndpointId { get; }

        public XmlDictionaryString WindowsSidClaim { get; }

        public XmlDictionaryString DenyOnlySidClaim { get; }

        public XmlDictionaryString X500DistinguishedNameClaim { get; }

        public XmlDictionaryString X509ThumbprintClaim { get; }

        public XmlDictionaryString NameClaim { get; }

        public XmlDictionaryString DnsClaim { get; }

        public XmlDictionaryString RsaClaim { get; }

        public XmlDictionaryString MailAddressClaim { get; }

        public XmlDictionaryString SystemClaim { get; }

        public XmlDictionaryString HashClaim { get; }

        public XmlDictionaryString SpnClaim { get; }

        public XmlDictionaryString UpnClaim { get; }

        public XmlDictionaryString UrlClaim { get; }

        public XmlDictionaryString Sid { get; }

    }
}
