// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF
{
    public class MessageSecurityOverHttp
    {
        internal const MessageCredentialType DefaultClientCredentialType = MessageCredentialType.Windows;
        internal const bool DefaultNegotiateServiceCredential = true;
        private MessageCredentialType _clientCredentialType;
        private SecurityAlgorithmSuite _algorithmSuite;
        private static readonly TimeSpan s_defaultServerIssuedTransitionTokenLifetime = TimeSpan.FromMinutes(15);
        public MessageSecurityOverHttp()
        {
            _clientCredentialType = DefaultClientCredentialType;
            NegotiateServiceCredential = DefaultNegotiateServiceCredential;
            _algorithmSuite = SecurityAlgorithmSuite.Default;
        }

        public MessageCredentialType ClientCredentialType
        {
            get { return _clientCredentialType; }
            set
            {
                if (!MessageCredentialTypeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _clientCredentialType = value;
            }
        }

        public bool NegotiateServiceCredential { get; set; }

        public SecurityAlgorithmSuite AlgorithmSuite
        {
            get { return _algorithmSuite; }
            set
            {
                _algorithmSuite = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                WasAlgorithmSuiteSet = true;
            }
        }

        internal bool WasAlgorithmSuiteSet { get; private set; }

        protected virtual bool IsSecureConversationEnabled()
        {
            return true;
        }

        public SecurityBindingElement CreateSecurityBindingElement(bool isSecureTransportMode, bool isReliableSession, MessageSecurityVersion version)
        {
            if (isReliableSession && !IsSecureConversationEnabled())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecureConversationRequiredByReliableSession)));
            }

            SecurityBindingElement result;
            bool isKerberosSelected = false;
            SecurityBindingElement oneShotSecurity;
            if (isSecureTransportMode)
            {
                switch (_clientCredentialType)
                {
                    case MessageCredentialType.None:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ClientCredentialTypeMustBeSpecifiedForMixedMode)));
                    case MessageCredentialType.UserName:
                        oneShotSecurity = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
                        break;
                    case MessageCredentialType.Certificate:
                        oneShotSecurity = SecurityBindingElement.CreateCertificateOverTransportBindingElement();
                        break;
                    case MessageCredentialType.Windows:
                       oneShotSecurity = SecurityBindingElement.CreateSspiNegotiationOverTransportBindingElement(true);
                       break;
                    //case MessageCredentialType.IssuedToken:
                    //    oneShotSecurity = SecurityBindingElement.CreateIssuedTokenOverTransportBindingElement(IssuedSecurityTokenParameters.CreateInfoCardParameters(new SecurityStandardsManager(new WSSecurityTokenSerializer(emitBspAttributes: true)), this.algorithmSuite));
                    //    break;
                    default:
                        Fx.Assert("unknown ClientCredentialType");
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                }
                if (IsSecureConversationEnabled())
                {
                    result = SecurityBindingElement.CreateSecureConversationBindingElement(oneShotSecurity, true);
                }
                else
                {
                    result = oneShotSecurity;
                }
            }
            else
            {
                throw new PlatformNotSupportedException();
                //TODO 
                //if (negotiateServiceCredential)
                //{
                //    switch (this.clientCredentialType)
                //    {
                //        case MessageCredentialType.None:
                //            oneShotSecurity = SecurityBindingElement.CreateSslNegotiationBindingElement(false, true);
                //            break;
                //        case MessageCredentialType.UserName:
                //            oneShotSecurity = SecurityBindingElement.CreateUserNameForSslBindingElement(true);
                //            break;
                //        case MessageCredentialType.Certificate:
                //            oneShotSecurity = SecurityBindingElement.CreateSslNegotiationBindingElement(true, true);
                //            break;
                //        case MessageCredentialType.Windows:
                //            oneShotSecurity = SecurityBindingElement.CreateSspiNegotiationBindingElement(true);
                //            break;
                //        case MessageCredentialType.IssuedToken:
                //            oneShotSecurity = SecurityBindingElement.CreateIssuedTokenForSslBindingElement(IssuedSecurityTokenParameters.CreateInfoCardParameters(new SecurityStandardsManager(new WSSecurityTokenSerializer(emitBspAttributes: true)), this.algorithmSuite), true);
                //            break;
                //        default:
                //            Fx.Assert("unknown ClientCredentialType");
                //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                //    }
                //}
                //else
                //{
                //    switch (this.clientCredentialType)
                //    {
                //        case MessageCredentialType.None:
                //            oneShotSecurity = SecurityBindingElement.CreateAnonymousForCertificateBindingElement();
                //            break;
                //        case MessageCredentialType.UserName:
                //            oneShotSecurity = SecurityBindingElement.CreateUserNameForCertificateBindingElement();
                //            break;
                //        case MessageCredentialType.Certificate:
                //            oneShotSecurity = SecurityBindingElement.CreateMutualCertificateBindingElement();
                //            break;
                //        case MessageCredentialType.Windows:
                //            oneShotSecurity = SecurityBindingElement.CreateKerberosBindingElement();
                //            isKerberosSelected = true;
                //            break;
                //        case MessageCredentialType.IssuedToken:
                //            oneShotSecurity = SecurityBindingElement.CreateIssuedTokenForCertificateBindingElement(IssuedSecurityTokenParameters.CreateInfoCardParameters(new SecurityStandardsManager(new WSSecurityTokenSerializer(emitBspAttributes: true)), this.algorithmSuite));
                //            break;
                //        default:
                //            Fx.Assert("unknown ClientCredentialType");
                //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                //    }
                //}
                //if (IsSecureConversationEnabled())
                //{
                //    result = SecurityBindingElement.CreateSecureConversationBindingElement(oneShotSecurity, true);
                //}
                //else
                //{
                //    result = oneShotSecurity;
                //}
            }

            // set the algorithm suite and issued token params if required
            if (WasAlgorithmSuiteSet || (!isKerberosSelected))
            {
                result.DefaultAlgorithmSuite = oneShotSecurity.DefaultAlgorithmSuite = AlgorithmSuite;
            }
            else if (isKerberosSelected)
            {
                result.DefaultAlgorithmSuite = oneShotSecurity.DefaultAlgorithmSuite = SecurityAlgorithmSuite.KerberosDefault;
            }

            result.IncludeTimestamp = true;
            oneShotSecurity.MessageSecurityVersion = version;
            result.MessageSecurityVersion = version;
            if (!isReliableSession)
            {
                result.LocalServiceSettings.ReconnectTransportOnFailure = false;
            }
            else
            {
                result.LocalServiceSettings.ReconnectTransportOnFailure = true;
            }

            if (IsSecureConversationEnabled())
            {
                oneShotSecurity.LocalServiceSettings.IssuedCookieLifetime = s_defaultServerIssuedTransitionTokenLifetime;
                //TODO SpNego when port, remove above and enable below.
                // issue the transition SCT for a short duration only
                // oneShotSecurity.LocalServiceSettings.IssuedCookieLifetime = SpnegoTokenAuthenticator.defaultServerIssuedTransitionTokenLifetime;
            }

            return result;
        }
    }
}
