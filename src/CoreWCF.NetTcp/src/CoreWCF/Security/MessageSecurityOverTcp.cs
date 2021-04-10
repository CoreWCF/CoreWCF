// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CoreWCF.Channels;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Security
{
    public sealed class MessageSecurityOverTcp
    {
        internal const MessageCredentialType DefaultClientCredentialType = MessageCredentialType.Windows;
        internal const string DefaultServerIssuedTransitionTokenLifetimeString = "00:15:00";

        private MessageCredentialType _clientCredentialType;
        private SecurityAlgorithmSuite _algorithmSuite;
        private bool _wasAlgorithmSuiteSet;

        public MessageSecurityOverTcp()
        {
            _clientCredentialType = DefaultClientCredentialType;
            _algorithmSuite = SecurityAlgorithmSuite.Default;
        }

        
        public MessageCredentialType ClientCredentialType
        {
            get { return this._clientCredentialType; }
            set
            {
                if (!MessageCredentialTypeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                this._clientCredentialType = value;
            }
        }

        public SecurityAlgorithmSuite AlgorithmSuite
        {
            get { return this._algorithmSuite; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                this._algorithmSuite = value;
                _wasAlgorithmSuiteSet = true;
            }
        }

        internal bool WasAlgorithmSuiteSet => this._wasAlgorithmSuiteSet;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public SecurityBindingElement CreateSecurityBindingElement(bool isSecureTransportMode, bool isReliableSession, BindingElement transportBindingElement)
        {
            SecurityBindingElement result;
            SecurityBindingElement oneShotSecurity;
            if (isSecureTransportMode)
            {
                switch (this._clientCredentialType)
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
                    case MessageCredentialType.IssuedToken:
                        throw new NotImplementedException();
                       // oneShotSecurity = SecurityBindingElement.CreateIssuedTokenOverTransportBindingElement(IssuedSecurityTokenParameters.CreateInfoCardParameters(new SecurityStandardsManager(), this.algorithmSuite));
                    default:
                        Fx.Assert("unknown ClientCredentialType");
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                }
                result = SecurityBindingElement.CreateSecureConversationBindingElement(oneShotSecurity);
            }
            else
            {
                throw new NotSupportedException();
               /* switch (this.clientCredentialType)
                {
                    case MessageCredentialType.None:
                        oneShotSecurity = SecurityBindingElement.CreateSslNegotiationBindingElement(false, true);
                        break;
                    case MessageCredentialType.UserName:
                        // require cancellation so that impersonation is possible
                        oneShotSecurity = SecurityBindingElement.CreateUserNameForSslBindingElement(true);
                        break;
                    case MessageCredentialType.Certificate:
                        oneShotSecurity = SecurityBindingElement.CreateSslNegotiationBindingElement(true, true);
                        break;
                    case MessageCredentialType.Windows:
                        // require cancellation so that impersonation is possible
                        oneShotSecurity = SecurityBindingElement.CreateSspiNegotiationBindingElement(true);
                        break;
                    case MessageCredentialType.IssuedToken:
                        oneShotSecurity = SecurityBindingElement.CreateIssuedTokenForSslBindingElement(IssuedSecurityTokenParameters.CreateInfoCardParameters(new SecurityStandardsManager(), this.algorithmSuite), true);
                        break;
                    default:
                        Fx.Assert("unknown ClientCredentialType");
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                }
                result = SecurityBindingElement.CreateSecureConversationBindingElement(oneShotSecurity, true);*/
            }

            // set the algorithm suite and issued token params if required
            result.DefaultAlgorithmSuite = oneShotSecurity.DefaultAlgorithmSuite = this.AlgorithmSuite;

            result.IncludeTimestamp = true;
            if (!isReliableSession)
            {
                result.LocalServiceSettings.ReconnectTransportOnFailure = false;
            }
            else
            {
                result.LocalServiceSettings.ReconnectTransportOnFailure = true;
            }

            // since a session is always bootstrapped, configure the transition sct to live for a short time only
            oneShotSecurity.LocalServiceSettings.IssuedCookieLifetime = TimeSpan.Parse(DefaultServerIssuedTransitionTokenLifetimeString, CultureInfo.InvariantCulture);
            result.MessageSecurityVersion = MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11;
            oneShotSecurity.MessageSecurityVersion = MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11;

            return result;
        }
    }
}
