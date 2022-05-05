// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Configuration
{
    public class SecurityElementBase : BindingElementExtensionElement
    {
        // if you add another variable, make sure to adjust: CopyFrom and UnMerge methods.
        private SecurityBindingElement _failedSecurityBindingElement;
        private bool _willX509IssuerReferenceAssertionBeWritten;
        private SecurityKeyType _templateKeyType = SecurityBindingDefaults.DefaultKeyType;

        internal SecurityElementBase()
        {
        }

        internal bool HasImportFailed { get { return _failedSecurityBindingElement != null; } }

        internal bool IsSecurityElementBootstrap { get; set; } // Used in serialization path to optimize Xml representation

        [ConfigurationProperty(ConfigurationStrings.DefaultAlgorithmSuite, DefaultValue = SecurityBindingDefaults.DefaultAlgorithmSuiteString)]
        [TypeConverter(typeof(SecurityAlgorithmSuiteConverter))]
        public SecurityAlgorithmSuite DefaultAlgorithmSuite
        {
            get { return (SecurityAlgorithmSuite)base[ConfigurationStrings.DefaultAlgorithmSuite]; }
            set { base[ConfigurationStrings.DefaultAlgorithmSuite] = value; }
        }

        //TODO If AsymmetricSecurityBindingElement is added 
        //[ConfigurationProperty(ConfigurationStrings.AllowSerializedSigningTokenOnReply, DefaultValue = SecurityBindingDefaults.DefaultAllowSerializedSigningTokenOnReply)]
        //public bool AllowSerializedSigningTokenOnReply
        //{
        //    get { return (bool)base[ConfigurationStrings.AllowSerializedSigningTokenOnReply]; }
        //    set { base[ConfigurationStrings.AllowSerializedSigningTokenOnReply] = value; }
        //}

        [ConfigurationProperty(ConfigurationStrings.EnableUnsecuredResponse, DefaultValue = SecurityBindingDefaults.DefaultEnableUnsecuredResponse)]
        public bool EnableUnsecuredResponse
        {
            get { return (bool)base[ConfigurationStrings.EnableUnsecuredResponse]; }
            set { base[ConfigurationStrings.EnableUnsecuredResponse] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.AuthenticationMode, DefaultValue = SecurityBindingDefaults.DefaultAuthenticationMode)]
        public AuthenticationMode AuthenticationMode
        {
            get { return (AuthenticationMode)base[ConfigurationStrings.AuthenticationMode]; }
            set { base[ConfigurationStrings.AuthenticationMode] = value; }
        }

        public override Type BindingElementType
        {
            get { return typeof(SecurityBindingElement); }
        }

        [ConfigurationProperty(ConfigurationStrings.RequireDerivedKeys, DefaultValue = SecurityBindingDefaults.DefaultRequireDerivedKeys)]
        public bool RequireDerivedKeys
        {
            get { return (bool)base[ConfigurationStrings.RequireDerivedKeys]; }
            set { base[ConfigurationStrings.RequireDerivedKeys] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.SecurityHeaderLayout, DefaultValue = SecurityBindingDefaults.DefaultSecurityHeaderLayout)]
        public SecurityHeaderLayout SecurityHeaderLayout
        {
            get { return (SecurityHeaderLayout)base[ConfigurationStrings.SecurityHeaderLayout]; }
            set { base[ConfigurationStrings.SecurityHeaderLayout] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.IncludeTimestamp, DefaultValue = SecurityBindingDefaults.DefaultIncludeTimestamp)]
        public bool IncludeTimestamp
        {
            get { return (bool)base[ConfigurationStrings.IncludeTimestamp]; }
            set { base[ConfigurationStrings.IncludeTimestamp] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.AllowInsecureTransport, DefaultValue = SecurityBindingDefaults.DefaultAllowInsecureTransport)]
        public bool AllowInsecureTransport
        {
            get { return (bool)base[ConfigurationStrings.AllowInsecureTransport]; }
            set { base[ConfigurationStrings.AllowInsecureTransport] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.KeyEntropyMode, DefaultValue = SecurityBindingDefaults.DefaultKeyEntropyMode)]
        public SecurityKeyEntropyMode KeyEntropyMode
        {
            get { return (SecurityKeyEntropyMode)base[ConfigurationStrings.KeyEntropyMode]; }
            set { base[ConfigurationStrings.KeyEntropyMode] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.IssuedTokenParameters)]
        public IssuedTokenParametersElement IssuedTokenParameters
        {
            get { return (IssuedTokenParametersElement)base[ConfigurationStrings.IssuedTokenParameters]; }
        }

        [ConfigurationProperty(ConfigurationStrings.LocalServiceSettings)]
        public LocalServiceSecuritySettingsElement LocalServiceSettings
        {
            get { return (LocalServiceSecuritySettingsElement)base[ConfigurationStrings.LocalServiceSettings]; }
        }

        [ConfigurationProperty(ConfigurationStrings.MessageProtectionOrder, DefaultValue = SecurityBindingDefaults.DefaultMessageProtectionOrder)]
        public MessageProtectionOrder MessageProtectionOrder
        {
            get { return (MessageProtectionOrder)base[ConfigurationStrings.MessageProtectionOrder]; }
            set { base[ConfigurationStrings.MessageProtectionOrder] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.ProtectTokens, DefaultValue = false)]
        public bool ProtectTokens
        {
            get { return (bool)base[ConfigurationStrings.ProtectTokens]; }
            set { base[ConfigurationStrings.ProtectTokens] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.MessageSecurityVersion, DefaultValue = ConfigurationStrings.Default)]
        [TypeConverter(typeof(MessageSecurityVersionConverter))]
        public MessageSecurityVersion MessageSecurityVersion
        {
            get { return (MessageSecurityVersion)base[ConfigurationStrings.MessageSecurityVersion]; }
            set { base[ConfigurationStrings.MessageSecurityVersion] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.RequireSecurityContextCancellation, DefaultValue = SecurityBindingDefaults.DefaultRequireCancellation)]
        public bool RequireSecurityContextCancellation
        {
            get { return (bool)base[ConfigurationStrings.RequireSecurityContextCancellation]; }
            set { base[ConfigurationStrings.RequireSecurityContextCancellation] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.RequireSignatureConfirmation, DefaultValue = SecurityBindingDefaults.DefaultRequireSignatureConfirmation)]
        public bool RequireSignatureConfirmation
        {
            get { return (bool)base[ConfigurationStrings.RequireSignatureConfirmation]; }
            set { base[ConfigurationStrings.RequireSignatureConfirmation] = value; }
        }

        [ConfigurationProperty(ConfigurationStrings.CanRenewSecurityContextToken, DefaultValue = SecurityBindingDefaults.DefaultCanRenewSession)]
        public bool CanRenewSecurityContextToken
        {
            get { return (bool)base[ConfigurationStrings.CanRenewSecurityContextToken]; }
            set { base[ConfigurationStrings.CanRenewSecurityContextToken] = value; }
        }

        public override void ApplyConfiguration(BindingElement bindingElement)
        {
            base.ApplyConfiguration(bindingElement);

            SecurityBindingElement sbe = (SecurityBindingElement)bindingElement;

            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.DefaultAlgorithmSuite].ValueOrigin)
                sbe.DefaultAlgorithmSuite = DefaultAlgorithmSuite;
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.IncludeTimestamp].ValueOrigin)
                sbe.IncludeTimestamp = IncludeTimestamp;
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.MessageSecurityVersion].ValueOrigin)
                sbe.MessageSecurityVersion = MessageSecurityVersion;
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.KeyEntropyMode].ValueOrigin)
                sbe.KeyEntropyMode = KeyEntropyMode;
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.SecurityHeaderLayout].ValueOrigin)
                sbe.SecurityHeaderLayout = SecurityHeaderLayout;
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.RequireDerivedKeys].ValueOrigin)
                sbe.SetKeyDerivation(RequireDerivedKeys);
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.AllowInsecureTransport].ValueOrigin)
                sbe.AllowInsecureTransport = AllowInsecureTransport;
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.EnableUnsecuredResponse].ValueOrigin)
                sbe.EnableUnsecuredResponse = EnableUnsecuredResponse;
            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.ProtectTokens].ValueOrigin)
                sbe.ProtectTokens = ProtectTokens;


            SymmetricSecurityBindingElement ssbe = sbe as SymmetricSecurityBindingElement;

            if (ssbe != null)
            {
                if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.MessageProtectionOrder].ValueOrigin)
                    ssbe.MessageProtectionOrder = MessageProtectionOrder;
                if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.RequireSignatureConfirmation].ValueOrigin)
                    ssbe.RequireSignatureConfirmation = RequireSignatureConfirmation;
                SecureConversationSecurityTokenParameters scParameters = ssbe.ProtectionTokenParameters as SecureConversationSecurityTokenParameters;
                if (scParameters != null)
                {
                    scParameters.CanRenewSession = CanRenewSecurityContextToken;
                }
            }

            //TODO If AsymmetricSecurityBindingElement is added 
            //AsymmetricSecurityBindingElement asbe = sbe as AsymmetricSecurityBindingElement;

            //if (asbe != null)
            //{
            //    if (PropertyValueOrigin.Default != this.ElementInformation.Properties[ConfigurationStrings.MessageProtectionOrder].ValueOrigin)
            //        asbe.MessageProtectionOrder = this.MessageProtectionOrder;
            //    if (PropertyValueOrigin.Default != this.ElementInformation.Properties[ConfigurationStrings.RequireSignatureConfirmation].ValueOrigin)
            //        asbe.RequireSignatureConfirmation = this.RequireSignatureConfirmation;
            //    if (PropertyValueOrigin.Default != this.ElementInformation.Properties[ConfigurationStrings.AllowSerializedSigningTokenOnReply].ValueOrigin)
            //        asbe.AllowSerializedSigningTokenOnReply = this.AllowSerializedSigningTokenOnReply;
            //}

            TransportSecurityBindingElement tsbe = sbe as TransportSecurityBindingElement;

            if (tsbe != null)
            {
                if (tsbe.EndpointSupportingTokenParameters.Endorsing.Count == 1)
                {
                    SecureConversationSecurityTokenParameters scParameters = tsbe.EndpointSupportingTokenParameters.Endorsing[0] as SecureConversationSecurityTokenParameters;
                    if (scParameters != null)
                    {
                        scParameters.CanRenewSession = CanRenewSecurityContextToken;
                    }
                }
            }

            if (PropertyValueOrigin.Default != ElementInformation.Properties[ConfigurationStrings.LocalServiceSettings].ValueOrigin)
            {
                LocalServiceSettings.ApplyConfiguration(sbe.LocalServiceSettings);
            }
        }

        public override void CopyFrom(ServiceModelExtensionElement from)
        {
            base.CopyFrom(from);

            SecurityElementBase source = (SecurityElementBase)from;

            //TODO If AsymmetricSecurityBindingElement is added 
            //if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.AllowSerializedSigningTokenOnReply].ValueOrigin)
            //    this.AllowSerializedSigningTokenOnReply = source.AllowSerializedSigningTokenOnReply;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.DefaultAlgorithmSuite].ValueOrigin)
                DefaultAlgorithmSuite = source.DefaultAlgorithmSuite;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.EnableUnsecuredResponse].ValueOrigin)
                EnableUnsecuredResponse = source.EnableUnsecuredResponse;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.AllowInsecureTransport].ValueOrigin)
                AllowInsecureTransport = source.AllowInsecureTransport;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.RequireDerivedKeys].ValueOrigin)
                RequireDerivedKeys = source.RequireDerivedKeys;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.IncludeTimestamp].ValueOrigin)
                IncludeTimestamp = source.IncludeTimestamp;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.IssuedTokenParameters].ValueOrigin)
                this.IssuedTokenParameters.Copy(source.IssuedTokenParameters);
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.MessageProtectionOrder].ValueOrigin)
                MessageProtectionOrder = source.MessageProtectionOrder;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.ProtectTokens].ValueOrigin)
                ProtectTokens = source.ProtectTokens;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.MessageSecurityVersion].ValueOrigin)
                MessageSecurityVersion = source.MessageSecurityVersion;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.RequireSignatureConfirmation].ValueOrigin)
                RequireSignatureConfirmation = source.RequireSignatureConfirmation;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.RequireSecurityContextCancellation].ValueOrigin)
                RequireSecurityContextCancellation = source.RequireSecurityContextCancellation;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.CanRenewSecurityContextToken].ValueOrigin)
                CanRenewSecurityContextToken = source.CanRenewSecurityContextToken;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.KeyEntropyMode].ValueOrigin)
                KeyEntropyMode = source.KeyEntropyMode;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.SecurityHeaderLayout].ValueOrigin)
                SecurityHeaderLayout = source.SecurityHeaderLayout;
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.LocalServiceSettings].ValueOrigin)
                LocalServiceSettings.CopyFrom(source.LocalServiceSettings);

            _failedSecurityBindingElement = source._failedSecurityBindingElement;
            _willX509IssuerReferenceAssertionBeWritten = source._willX509IssuerReferenceAssertionBeWritten;
        }

        protected internal override BindingElement CreateBindingElement()
        {
            return CreateBindingElement(false);
        }

        protected internal virtual BindingElement CreateBindingElement(bool createTemplateOnly)
        {
            SecurityBindingElement result;
            switch (AuthenticationMode)
            {
                //case AuthenticationMode.AnonymousForCertificate:
                //    result = SecurityBindingElement.CreateAnonymousForCertificateBindingElement();
                //    break;
                //case AuthenticationMode.AnonymousForSslNegotiated:
                //    result = SecurityBindingElement.CreateSslNegotiationBindingElement(false, this.RequireSecurityContextCancellation);
                //    break;
                case AuthenticationMode.CertificateOverTransport:
                    result = SecurityBindingElement.CreateCertificateOverTransportBindingElement(MessageSecurityVersion);
                    break;
                //case AuthenticationMode.IssuedToken:
                //    result = SecurityBindingElement.CreateIssuedTokenBindingElement(this.IssuedTokenParameters.Create(createTemplateOnly, _templateKeyType));
                //    break;
                case AuthenticationMode.IssuedTokenForCertificate:
                    result = SecurityBindingElement.CreateIssuedTokenForCertificateBindingElement(this.IssuedTokenParameters.Create(createTemplateOnly, _templateKeyType));
                    break;
                case AuthenticationMode.IssuedTokenForSslNegotiated:
                    result = SecurityBindingElement.CreateIssuedTokenForSslBindingElement(this.IssuedTokenParameters.Create(createTemplateOnly, _templateKeyType), this.RequireSecurityContextCancellation);
                    break;
                case AuthenticationMode.IssuedTokenOverTransport:
                    result = SecurityBindingElement.CreateIssuedTokenOverTransportBindingElement(this.IssuedTokenParameters.Create(createTemplateOnly, _templateKeyType));
                    break;
                //case AuthenticationMode.Kerberos:
                //    result = SecurityBindingElement.CreateKerberosBindingElement();
                //    break;
                //case AuthenticationMode.KerberosOverTransport:
                //    result = SecurityBindingElement.CreateKerberosOverTransportBindingElement();
                //    break;
                //case AuthenticationMode.MutualCertificateDuplex:
                //    result = SecurityBindingElement.CreateMutualCertificateDuplexBindingElement(this.MessageSecurityVersion);
                //    break;
                //case AuthenticationMode.MutualCertificate:
                //    result = SecurityBindingElement.CreateMutualCertificateBindingElement(this.MessageSecurityVersion);
                //    break;
                //case AuthenticationMode.MutualSslNegotiated:
                //    result = SecurityBindingElement.CreateSslNegotiationBindingElement(true, this.RequireSecurityContextCancellation);
                //    break;
                //case AuthenticationMode.SspiNegotiated:
                //    result = SecurityBindingElement.CreateSspiNegotiationBindingElement(this.RequireSecurityContextCancellation);
                //    break;
                //case AuthenticationMode.UserNameForCertificate:
                //    result = SecurityBindingElement.CreateUserNameForCertificateBindingElement();
                //    break;
                //case AuthenticationMode.UserNameForSslNegotiated:
                //    result = SecurityBindingElement.CreateUserNameForSslBindingElement(this.RequireSecurityContextCancellation);
                //    break;
                case AuthenticationMode.UserNameOverTransport:
                    result = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
                    break;
                case AuthenticationMode.SspiNegotiatedOverTransport:
                    result = SecurityBindingElement.CreateSspiNegotiationOverTransportBindingElement(RequireSecurityContextCancellation);
                    break;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(AuthenticationMode), (int)AuthenticationMode, typeof(AuthenticationMode)));
            }

            ApplyConfiguration(result);

            return result;
        }

        protected void AddBindingTemplate(Dictionary<AuthenticationMode, SecurityBindingElement> bindingTemplates, AuthenticationMode mode)
        {
            AuthenticationMode = mode;
            try
            {
                bindingTemplates[mode] = (SecurityBindingElement)CreateBindingElement(true);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
            }
        }

        private static bool AreTokenParametersMatching(SecurityTokenParameters p1, SecurityTokenParameters p2, bool skipRequireDerivedKeysComparison, bool exactMessageSecurityVersion)
        {
            if (p1 == null || p2 == null)
                return false;

            if (p1.GetType() != p2.GetType())
                return false;

            if (p1.InclusionMode != p2.InclusionMode)
                return false;

            if (skipRequireDerivedKeysComparison == false && p1.RequireDerivedKeys != p2.RequireDerivedKeys)
                return false;

            if (p1.ReferenceStyle != p2.ReferenceStyle)
                return false;

            // mutual ssl and anonymous ssl differ in the client cert requirement
            if (p1 is SslSecurityTokenParameters)
            {
                if (((SslSecurityTokenParameters)p1).RequireClientCertificate != ((SslSecurityTokenParameters)p2).RequireClientCertificate)
                    return false;
            }
            else if (p1 is SecureConversationSecurityTokenParameters)
            {
                SecureConversationSecurityTokenParameters sc1 = (SecureConversationSecurityTokenParameters)p1;
                SecureConversationSecurityTokenParameters sc2 = (SecureConversationSecurityTokenParameters)p2;

                if (sc1.RequireCancellation != sc2.RequireCancellation)
                    return false;

                if (sc1.CanRenewSession != sc2.CanRenewSession)
                    return false;


                if (!AreBindingsMatching(sc1.BootstrapSecurityBindingElement, sc2.BootstrapSecurityBindingElement, exactMessageSecurityVersion))
                    return false;
            }
            else if (p1 is IssuedSecurityTokenParameters)
            {
                if (((IssuedSecurityTokenParameters)p1).KeyType != ((IssuedSecurityTokenParameters)p2).KeyType)
                    return false;
            }

            return true;
        }

        private static bool AreTokenParameterCollectionsMatching(Collection<SecurityTokenParameters> c1, Collection<SecurityTokenParameters> c2, bool exactMessageSecurityVersion)
        {
            if (c1.Count != c2.Count)
                return false;

            for (int i = 0; i < c1.Count; i++)
                if (!AreTokenParametersMatching(c1[i], c2[i], true, exactMessageSecurityVersion))
                    return false;

            return true;
        }

        internal static bool AreBindingsMatching(SecurityBindingElement b1, SecurityBindingElement b2)
        {
            return AreBindingsMatching(b1, b2, true);
        }

        internal static bool AreBindingsMatching(SecurityBindingElement b1, SecurityBindingElement b2, bool exactMessageSecurityVersion)
        {
            if (b1 == null || b2 == null)
                return b1 == b2;

            if (b1.GetType() != b2.GetType())
                return false;

            if (b1.MessageSecurityVersion != b2.MessageSecurityVersion)
            {
                // exactMessageSecurityVersion meant that BSP mismatch could be ignored
                if (exactMessageSecurityVersion)
                    return false;

                if (b1.MessageSecurityVersion.SecurityVersion != b2.MessageSecurityVersion.SecurityVersion
                 || b1.MessageSecurityVersion.TrustVersion != b2.MessageSecurityVersion.TrustVersion
                 || b1.MessageSecurityVersion.SecureConversationVersion != b2.MessageSecurityVersion.SecureConversationVersion
                 || b1.MessageSecurityVersion.SecurityPolicyVersion != b2.MessageSecurityVersion.SecurityPolicyVersion)
                {
                    return false;
                }
            }

            if (b1.SecurityHeaderLayout != b2.SecurityHeaderLayout)
                return false;

            if (b1.DefaultAlgorithmSuite != b2.DefaultAlgorithmSuite)
                return false;

            if (b1.IncludeTimestamp != b2.IncludeTimestamp)
                return false;

            if (b1.SecurityHeaderLayout != b2.SecurityHeaderLayout)
                return false;

            if (b1.KeyEntropyMode != b2.KeyEntropyMode)
                return false;

            if (!AreTokenParameterCollectionsMatching(b1.EndpointSupportingTokenParameters.Endorsing, b2.EndpointSupportingTokenParameters.Endorsing, exactMessageSecurityVersion))
                return false;

            if (!AreTokenParameterCollectionsMatching(b1.EndpointSupportingTokenParameters.SignedEncrypted, b2.EndpointSupportingTokenParameters.SignedEncrypted, exactMessageSecurityVersion))
                return false;

            if (!AreTokenParameterCollectionsMatching(b1.EndpointSupportingTokenParameters.Signed, b2.EndpointSupportingTokenParameters.Signed, exactMessageSecurityVersion))
                return false;

            if (!AreTokenParameterCollectionsMatching(b1.EndpointSupportingTokenParameters.SignedEndorsing, b2.EndpointSupportingTokenParameters.SignedEndorsing, exactMessageSecurityVersion))
                return false;

            if (b1.OperationSupportingTokenParameters.Count != b2.OperationSupportingTokenParameters.Count)
                return false;

            foreach (KeyValuePair<string, SupportingTokenParameters> operation1 in b1.OperationSupportingTokenParameters)
            {
                if (!b2.OperationSupportingTokenParameters.ContainsKey(operation1.Key))
                    return false;

                SupportingTokenParameters stp2 = b2.OperationSupportingTokenParameters[operation1.Key];

                if (!AreTokenParameterCollectionsMatching(operation1.Value.Endorsing, stp2.Endorsing, exactMessageSecurityVersion))
                    return false;

                if (!AreTokenParameterCollectionsMatching(operation1.Value.SignedEncrypted, stp2.SignedEncrypted, exactMessageSecurityVersion))
                    return false;

                if (!AreTokenParameterCollectionsMatching(operation1.Value.Signed, stp2.Signed, exactMessageSecurityVersion))
                    return false;

                if (!AreTokenParameterCollectionsMatching(operation1.Value.SignedEndorsing, stp2.SignedEndorsing, exactMessageSecurityVersion))
                    return false;
            }

            SymmetricSecurityBindingElement ssbe1 = b1 as SymmetricSecurityBindingElement;
            if (ssbe1 != null)
            {
                SymmetricSecurityBindingElement ssbe2 = (SymmetricSecurityBindingElement)b2;

                if (ssbe1.MessageProtectionOrder != ssbe2.MessageProtectionOrder)
                    return false;

                if (!AreTokenParametersMatching(ssbe1.ProtectionTokenParameters, ssbe2.ProtectionTokenParameters, false, exactMessageSecurityVersion))
                    return false;
            }

            //TODO If AsymmetricKey is supported
            //AsymmetricSecurityBindingElement asbe1 = b1 as AsymmetricSecurityBindingElement;
            //if (asbe1 != null)
            //{
            //    AsymmetricSecurityBindingElement asbe2 = (AsymmetricSecurityBindingElement)b2;

            //    if (asbe1.MessageProtectionOrder != asbe2.MessageProtectionOrder)
            //        return false;

            //    if (asbe1.RequireSignatureConfirmation != asbe2.RequireSignatureConfirmation)
            //        return false;

            //    if (!AreTokenParametersMatching(asbe1.InitiatorTokenParameters, asbe2.InitiatorTokenParameters, true, exactMessageSecurityVersion)
            //        || !AreTokenParametersMatching(asbe1.RecipientTokenParameters, asbe2.RecipientTokenParameters, true, exactMessageSecurityVersion))
            //        return false;
            //}

            return true;
        }

        protected virtual void AddBindingTemplates(Dictionary<AuthenticationMode, SecurityBindingElement> bindingTemplates)
        {
            //TODO As Authentication Modes are added
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.AnonymousForCertificate);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.AnonymousForSslNegotiated);
            AddBindingTemplate(bindingTemplates, AuthenticationMode.CertificateOverTransport);
            //if (_templateKeyType == SecurityKeyType.SymmetricKey)
            //{
            //    AddBindingTemplate(bindingTemplates, AuthenticationMode.IssuedToken);
            //}
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.IssuedTokenForCertificate);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.IssuedTokenForSslNegotiated);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.IssuedTokenOverTransport);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.Kerberos);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.KerberosOverTransport);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.MutualCertificate);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.MutualCertificateDuplex);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.MutualSslNegotiated);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.SspiNegotiated);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.UserNameForCertificate);
            //AddBindingTemplate(bindingTemplates, AuthenticationMode.UserNameForSslNegotiated);
            AddBindingTemplate(bindingTemplates, AuthenticationMode.UserNameOverTransport);
            AddBindingTemplate(bindingTemplates, AuthenticationMode.SspiNegotiatedOverTransport);
        }

        private bool TryInitializeAuthenticationMode(SecurityBindingElement sbe)
        {
            bool result;

            if (sbe.OperationSupportingTokenParameters.Count > 0)
                result = false;
            else
            {
                SetIssuedTokenKeyType(sbe);

                Dictionary<AuthenticationMode, SecurityBindingElement> bindingTemplates = new Dictionary<AuthenticationMode, SecurityBindingElement>();
                AddBindingTemplates(bindingTemplates);

                result = false;
                foreach (AuthenticationMode mode in bindingTemplates.Keys)
                {
                    SecurityBindingElement candidate = bindingTemplates[mode];
                    if (AreBindingsMatching(sbe, candidate))
                    {
                        AuthenticationMode = mode;
                        result = true;
                        break;
                    }
                }
            }

            return result;
        }

        private void SetIssuedTokenKeyType(SecurityBindingElement sbe)
        {
            // Set the keyType for building the template for IssuedToken binding.
            // The reason is the different supporting token is defined depending on keyType.
            if (sbe.EndpointSupportingTokenParameters.Endorsing.Count > 0 &&
                sbe.EndpointSupportingTokenParameters.Endorsing[0] is IssuedSecurityTokenParameters)
            {
                _templateKeyType = ((IssuedSecurityTokenParameters)sbe.EndpointSupportingTokenParameters.Endorsing[0]).KeyType;
            }
            else if (sbe.EndpointSupportingTokenParameters.Signed.Count > 0 &&
                sbe.EndpointSupportingTokenParameters.Signed[0] is IssuedSecurityTokenParameters)
            {
                _templateKeyType = ((IssuedSecurityTokenParameters)sbe.EndpointSupportingTokenParameters.Signed[0]).KeyType;
            }
            else if (sbe.EndpointSupportingTokenParameters.SignedEncrypted.Count > 0 &&
                sbe.EndpointSupportingTokenParameters.SignedEncrypted[0] is IssuedSecurityTokenParameters)
            {
                _templateKeyType = ((IssuedSecurityTokenParameters)sbe.EndpointSupportingTokenParameters.SignedEncrypted[0]).KeyType;
            }
            else
            {
                _templateKeyType = SecurityBindingDefaults.DefaultKeyType;
            }
        }

        protected virtual void InitializeNestedTokenParameterSettings(SecurityTokenParameters sp, bool initializeNestedBindings)
        {
            if (sp is SspiSecurityTokenParameters)
                SetPropertyValueIfNotDefaultValue(ConfigurationStrings.RequireSecurityContextCancellation, ((SspiSecurityTokenParameters)sp).RequireCancellation);
            else if (sp is SslSecurityTokenParameters)
                SetPropertyValueIfNotDefaultValue(ConfigurationStrings.RequireSecurityContextCancellation, ((SslSecurityTokenParameters)sp).RequireCancellation);
            //TODO: Implement IssuedTokenParameters
            //else if (sp is IssuedSecurityTokenParameters)
            //    this.IssuedTokenParameters.InitializeFrom((IssuedSecurityTokenParameters)sp, initializeNestedBindings);
        }

        internal void InitializeFrom(BindingElement bindingElement, bool initializeNestedBindings)
        {
            if (bindingElement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bindingElement));
            }
            SecurityBindingElement sbe = (SecurityBindingElement)bindingElement;

            // Can't apply default value optimization to properties like DefaultAlgorithmSuite because the defaults are computed at runtime and don't match config defaults
            DefaultAlgorithmSuite = sbe.DefaultAlgorithmSuite;
            IncludeTimestamp = sbe.IncludeTimestamp;
            if (sbe.MessageSecurityVersion != MessageSecurityVersion.Default)
            {
                MessageSecurityVersion = sbe.MessageSecurityVersion;
            }
            // Still safe to apply the optimization here because the runtime defaults are the same as config defaults in all cases
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.KeyEntropyMode, sbe.KeyEntropyMode);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.SecurityHeaderLayout, sbe.SecurityHeaderLayout);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.ProtectTokens, sbe.ProtectTokens);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.AllowInsecureTransport, sbe.AllowInsecureTransport);
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.EnableUnsecuredResponse, sbe.EnableUnsecuredResponse);


            bool? requireDerivedKeys = new bool?();

            if (sbe.EndpointSupportingTokenParameters.Endorsing.Count == 1)
            {
                InitializeNestedTokenParameterSettings(sbe.EndpointSupportingTokenParameters.Endorsing[0], initializeNestedBindings);
            }
            else if (sbe.EndpointSupportingTokenParameters.SignedEncrypted.Count == 1)
            {
                InitializeNestedTokenParameterSettings(sbe.EndpointSupportingTokenParameters.SignedEncrypted[0], initializeNestedBindings);
            }
            else if (sbe.EndpointSupportingTokenParameters.Signed.Count == 1)
            {
                InitializeNestedTokenParameterSettings(sbe.EndpointSupportingTokenParameters.Signed[0], initializeNestedBindings);
            }

            bool initializationFailure = false;

            foreach (SecurityTokenParameters t in sbe.EndpointSupportingTokenParameters.Endorsing)
            {
                //if (t.HasAsymmetricKey == false)//TODO If AsymmetricKey is supported 
                {
                    if (requireDerivedKeys.HasValue && requireDerivedKeys.Value != t.RequireDerivedKeys)
                        initializationFailure = true;
                    else
                        requireDerivedKeys = t.RequireDerivedKeys;
                }
            }

            SymmetricSecurityBindingElement ssbe = sbe as SymmetricSecurityBindingElement;
            if (ssbe != null)
            {
                SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MessageProtectionOrder, ssbe.MessageProtectionOrder);
                RequireSignatureConfirmation = ssbe.RequireSignatureConfirmation;
                if (ssbe.ProtectionTokenParameters != null)
                {
                    InitializeNestedTokenParameterSettings(ssbe.ProtectionTokenParameters, initializeNestedBindings);
                    if (requireDerivedKeys.HasValue && requireDerivedKeys.Value != ssbe.ProtectionTokenParameters.RequireDerivedKeys)
                        initializationFailure = true;
                    else
                        requireDerivedKeys = ssbe.ProtectionTokenParameters.RequireDerivedKeys;
                }
            }
            else
            {
                //TODO If AsymmetricKey is supported
                //AsymmetricSecurityBindingElement asbe = sbe as AsymmetricSecurityBindingElement;
                //if (asbe != null)
                //{
                //    SetPropertyValueIfNotDefaultValue(ConfigurationStrings.MessageProtectionOrder, asbe.MessageProtectionOrder);
                //    this.RequireSignatureConfirmation = asbe.RequireSignatureConfirmation;
                //    if (asbe.InitiatorTokenParameters != null)
                //    {
                //        this.InitializeNestedTokenParameterSettings(asbe.InitiatorTokenParameters, initializeNestedBindings);

                //        //
                //        // Copy the derived key token bool flag from the token parameters. The token parameter was set from
                //        // importing WSDL during SecurityBindingElementImporter.ImportPolicy time
                //        //
                //        if (requireDerivedKeys.HasValue && requireDerivedKeys.Value != asbe.InitiatorTokenParameters.RequireDerivedKeys)
                //            initializationFailure = true;
                //        else
                //            requireDerivedKeys = asbe.InitiatorTokenParameters.RequireDerivedKeys;
                //    }
                //}
            }

            _willX509IssuerReferenceAssertionBeWritten = DoesSecurityBindingElementContainClauseTypeofIssuerSerial(sbe);
            RequireDerivedKeys = requireDerivedKeys.GetValueOrDefault(SecurityBindingDefaults.DefaultRequireDerivedKeys);
            LocalServiceSettings.InitializeFrom(sbe.LocalServiceSettings);

            if (!initializationFailure)
                initializationFailure = !TryInitializeAuthenticationMode(sbe);

            if (initializationFailure)
                _failedSecurityBindingElement = sbe;
        }

        protected internal override void InitializeFrom(BindingElement bindingElement)
        {
            InitializeFrom(bindingElement, true);
        }

        /// <summary>
        /// returns true if one of the xxxSupportingTokenParameters.yyy is of type IssuerSerial
        /// </summary>
        /// <param name="sbe"></param>
        /// <returns></returns>
        private bool DoesSecurityBindingElementContainClauseTypeofIssuerSerial(SecurityBindingElement sbe)
        {
            if (sbe == null)
                return false;

            if (sbe is SymmetricSecurityBindingElement)
            {
                X509SecurityTokenParameters tokenParamameters = ((SymmetricSecurityBindingElement)sbe).ProtectionTokenParameters as X509SecurityTokenParameters;
                if (tokenParamameters != null && tokenParamameters.X509ReferenceStyle == X509KeyIdentifierClauseType.IssuerSerial)
                    return true;
            }
            //TODO if AsymmetricSecurityBindingElement is added
            //else if (sbe is AsymmetricSecurityBindingElement)
            //{
            //    X509SecurityTokenParameters initiatorParamameters = ((AsymmetricSecurityBindingElement)sbe).InitiatorTokenParameters as X509SecurityTokenParameters;
            //    if (initiatorParamameters != null && initiatorParamameters.X509ReferenceStyle == X509KeyIdentifierClauseType.IssuerSerial)
            //        return true;

            //    X509SecurityTokenParameters recepientParamameters = ((AsymmetricSecurityBindingElement)sbe).RecipientTokenParameters as X509SecurityTokenParameters;
            //    if (recepientParamameters != null && recepientParamameters.X509ReferenceStyle == X509KeyIdentifierClauseType.IssuerSerial)
            //        return true;
            //}

            if (DoesX509TokenParametersContainClauseTypeofIssuerSerial(sbe.EndpointSupportingTokenParameters.Endorsing))
                return true;

            if (DoesX509TokenParametersContainClauseTypeofIssuerSerial(sbe.EndpointSupportingTokenParameters.Signed))
                return true;

            if (DoesX509TokenParametersContainClauseTypeofIssuerSerial(sbe.EndpointSupportingTokenParameters.SignedEncrypted))
                return true;

            if (DoesX509TokenParametersContainClauseTypeofIssuerSerial(sbe.EndpointSupportingTokenParameters.SignedEndorsing))
                return true;

            if (DoesX509TokenParametersContainClauseTypeofIssuerSerial(sbe.OptionalEndpointSupportingTokenParameters.Endorsing))
                return true;

            if (DoesX509TokenParametersContainClauseTypeofIssuerSerial(sbe.OptionalEndpointSupportingTokenParameters.Signed))
                return true;

            if (DoesX509TokenParametersContainClauseTypeofIssuerSerial(sbe.OptionalEndpointSupportingTokenParameters.SignedEncrypted))
                return true;

            if (DoesX509TokenParametersContainClauseTypeofIssuerSerial(sbe.OptionalEndpointSupportingTokenParameters.SignedEndorsing))
                return true;

            return false;
        }

        private bool DoesX509TokenParametersContainClauseTypeofIssuerSerial(Collection<SecurityTokenParameters> tokenParameters)
        {
            foreach (SecurityTokenParameters tokenParameter in tokenParameters)
            {
                X509SecurityTokenParameters x509TokenParameter = tokenParameter as X509SecurityTokenParameters;
                if (x509TokenParameter != null)
                {
                    if (x509TokenParameter.X509ReferenceStyle == X509KeyIdentifierClauseType.IssuerSerial)
                        return true;
                }
            }

            return false;
        }

        protected override bool SerializeToXmlElement(XmlWriter writer, String elementName)
        {
            bool result;

            if (_failedSecurityBindingElement != null && writer != null)
            {
                writer.WriteComment(SR.Format(SR.ConfigurationSchemaInsuffientForSecurityBindingElementInstance));
                writer.WriteComment(_failedSecurityBindingElement.ToString());
                result = true;
            }
            else
            {
                if (writer != null && _willX509IssuerReferenceAssertionBeWritten)
                    writer.WriteComment(SR.Format(SR.ConfigurationSchemaContainsX509IssuerSerialReference));

                result = base.SerializeToXmlElement(writer, elementName);
            }

            return result;
        }

        protected override bool SerializeElement(XmlWriter writer, bool serializeCollectionKey)
        {
            bool nontrivial = base.SerializeElement(writer, serializeCollectionKey);

            // A SecurityElement can copy properties from a "bootstrap" SecurityBaseElement.
            // In this case, a trivial bootstrap (no properties set) is equivalent to not having one at all so we can omit it.
            Func<PropertyInformation, bool> nontrivialProperty = property => property.ValueOrigin == PropertyValueOrigin.SetHere;
            if (IsSecurityElementBootstrap && !ElementInformation.Properties.OfType<PropertyInformation>().Any(nontrivialProperty))
            {
                nontrivial = false;
            }
            return nontrivial;
        }


        protected override void Unmerge(ConfigurationElement sourceElement, ConfigurationElement parentElement, ConfigurationSaveMode saveMode)
        {
            if (sourceElement is SecurityElementBase)
            {
                _failedSecurityBindingElement = ((SecurityElementBase)sourceElement)._failedSecurityBindingElement;
                _willX509IssuerReferenceAssertionBeWritten = ((SecurityElementBase)sourceElement)._willX509IssuerReferenceAssertionBeWritten;
            }

            base.Unmerge(sourceElement, parentElement, saveMode);
        }
    }
}
