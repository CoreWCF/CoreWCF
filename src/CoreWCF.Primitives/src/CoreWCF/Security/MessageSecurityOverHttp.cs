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

        MessageCredentialType clientCredentialType;
        bool negotiateServiceCredential;
        SecurityAlgorithmSuite algorithmSuite;
        bool wasAlgorithmSuiteSet;
        private static readonly TimeSpan defaultServerIssuedTransitionTokenLifetime = TimeSpan.FromMinutes(15);
        public MessageSecurityOverHttp()
        {
            clientCredentialType = DefaultClientCredentialType;
            negotiateServiceCredential = DefaultNegotiateServiceCredential;
            algorithmSuite = SecurityAlgorithmSuite.Default;
        }

        public MessageCredentialType ClientCredentialType
        {
            get { return this.clientCredentialType; }
            set
            {
                if (!MessageCredentialTypeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value"));
                }
                this.clientCredentialType = value;
            }
        }

        public bool NegotiateServiceCredential
        {
            get { return this.negotiateServiceCredential; }
            set { this.negotiateServiceCredential = value; }
        }

        public SecurityAlgorithmSuite AlgorithmSuite
        {
            get { return this.algorithmSuite; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");
                }
                this.algorithmSuite = value;
                wasAlgorithmSuiteSet = true;
            }
        }

        internal bool WasAlgorithmSuiteSet
        {
            get { return this.wasAlgorithmSuiteSet; }
        }

        protected virtual bool IsSecureConversationEnabled()
        {
            return true;
        }

        public SecurityBindingElement CreateSecurityBindingElement(bool isSecureTransportMode, bool isReliableSession, MessageSecurityVersion version)
        {
            if (isReliableSession && !this.IsSecureConversationEnabled())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecureConversationRequiredByReliableSession)));
            }

            SecurityBindingElement result;
            SecurityBindingElement oneShotSecurity = null;

            bool isKerberosSelected = false;
            bool emitBspAttributes = true;
            if (isSecureTransportMode)
            {
                switch (this.clientCredentialType)
                {
                    case MessageCredentialType.None:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ClientCredentialTypeMustBeSpecifiedForMixedMode)));
                    case MessageCredentialType.UserName:
                        oneShotSecurity = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
                        break;
                    case MessageCredentialType.Certificate:
                        oneShotSecurity = SecurityBindingElement.CreateCertificateOverTransportBindingElement();
                        break;
                    //case MessageCredentialType.Windows:
                    //    oneShotSecurity = SecurityBindingElement.CreateSspiNegotiationOverTransportBindingElement(true);
                    //    break;
                    //case MessageCredentialType.IssuedToken:
                    //    oneShotSecurity = SecurityBindingElement.CreateIssuedTokenOverTransportBindingElement(IssuedSecurityTokenParameters.CreateInfoCardParameters(new SecurityStandardsManager(new WSSecurityTokenSerializer(emitBspAttributes)), this.algorithmSuite));
                    //    break;
                    default:
                        Fx.Assert("unknown ClientCredentialType");
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                }
                if (this.IsSecureConversationEnabled())
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
                //            oneShotSecurity = SecurityBindingElement.CreateIssuedTokenForSslBindingElement(IssuedSecurityTokenParameters.CreateInfoCardParameters(new SecurityStandardsManager(new WSSecurityTokenSerializer(emitBspAttributes)), this.algorithmSuite), true);
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
                //            oneShotSecurity = SecurityBindingElement.CreateIssuedTokenForCertificateBindingElement(IssuedSecurityTokenParameters.CreateInfoCardParameters(new SecurityStandardsManager(new WSSecurityTokenSerializer(emitBspAttributes)), this.algorithmSuite));
                //            break;
                //        default:
                //            Fx.Assert("unknown ClientCredentialType");
                //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                //    }
                //}
                if (this.IsSecureConversationEnabled())
                {
                    result = SecurityBindingElement.CreateSecureConversationBindingElement(oneShotSecurity, true);
                }
                else
                {
                    result = oneShotSecurity;
                }
            }

            // set the algorithm suite and issued token params if required
            if (wasAlgorithmSuiteSet || (!isKerberosSelected))
            {
                result.DefaultAlgorithmSuite = oneShotSecurity.DefaultAlgorithmSuite = this.AlgorithmSuite;
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

            if (this.IsSecureConversationEnabled())
            {
                oneShotSecurity.LocalServiceSettings.IssuedCookieLifetime = defaultServerIssuedTransitionTokenLifetime;
                //TODO SpNego when port, remove above and enable below.
                // issue the transition SCT for a short duration only
                // oneShotSecurity.LocalServiceSettings.IssuedCookieLifetime = SpnegoTokenAuthenticator.defaultServerIssuedTransitionTokenLifetime;
            }

            return result;
        }

    }
}
