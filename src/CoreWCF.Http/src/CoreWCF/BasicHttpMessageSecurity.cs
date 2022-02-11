// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;
using CoreWCF.Channels;
using CoreWCF.Security;
using System.ComponentModel;
using System;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public sealed class BasicHttpMessageSecurity
    {
        internal const BasicHttpMessageCredentialType DefaultClientCredentialType = BasicHttpMessageCredentialType.UserName;
        private BasicHttpMessageCredentialType _clientCredentialType;
        private SecurityAlgorithmSuite _algorithmSuite;

        public BasicHttpMessageSecurity()
        {
            _clientCredentialType = DefaultClientCredentialType;
            _algorithmSuite = SecurityAlgorithmSuite.Default;
        }

        public BasicHttpMessageCredentialType ClientCredentialType
        {
            get { return _clientCredentialType; }
            set
            {
                if (!BasicHttpMessageCredentialTypeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }
                _clientCredentialType = value;
            }
        }

        public SecurityAlgorithmSuite AlgorithmSuite
        {
            get { return _algorithmSuite; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                _algorithmSuite = value;
            }
        }

        // if any changes are made to this method, please reflect them in the corresponding TryCrete() method
        internal SecurityBindingElement CreateMessageSecurity(bool isSecureTransportMode)
        {
            SecurityBindingElement result;

            if (isSecureTransportMode)
            {
                MessageSecurityVersion version = MessageSecurityVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10;
                switch (_clientCredentialType)
                {
                    case BasicHttpMessageCredentialType.Certificate:
                        result = SecurityBindingElement.CreateCertificateOverTransportBindingElement(version);
                        break;
                    case BasicHttpMessageCredentialType.UserName:
                        result = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
                        result.MessageSecurityVersion = version;
                        break;
                    default:
                        Fx.Assert("Unsupported basic http message credential type");
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
                }
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedSecuritySetting, "Mode", BasicHttpSecurityMode.Message)));
                //if (_clientCredentialType != BasicHttpMessageCredentialType.Certificate)
                //{
                //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.BasicHttpMessageSecurityRequiresCertificate)));
                //}
                //result = SecurityBindingElement.CreateMutualCertificateBindingElement(MessageSecurityVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10, true);
            }

            result.DefaultAlgorithmSuite = AlgorithmSuite;
            result.SecurityHeaderLayout = SecurityHeaderLayout.Lax;
            result.SetKeyDerivation(false);
           // result.DoNotEmitTrust = true;

            return result;
        }

        internal bool InternalShouldSerialize()
        {
            return ShouldSerializeAlgorithmSuite()
                || ShouldSerializeClientCredentialType();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ShouldSerializeAlgorithmSuite()
        {
            return _algorithmSuite.GetType() != SecurityAlgorithmSuite.Default.GetType();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ShouldSerializeClientCredentialType()
        {
            return _clientCredentialType != DefaultClientCredentialType;
        }
    }
}
