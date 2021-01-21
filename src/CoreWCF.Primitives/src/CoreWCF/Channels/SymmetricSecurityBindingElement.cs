// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Net.Security;
using System.Text;
using CoreWCF.Configuration;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Channels
{
    public sealed class SymmetricSecurityBindingElement : SecurityBindingElement //, IPolicyExportExtension
    {
        private MessageProtectionOrder _messageProtectionOrder;
        private SecurityTokenParameters _protectionTokenParameters;
        private bool _requireSignatureConfirmation;

        private SymmetricSecurityBindingElement(SymmetricSecurityBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _messageProtectionOrder = elementToBeCloned._messageProtectionOrder;
            if (elementToBeCloned._protectionTokenParameters != null)
            {
                _protectionTokenParameters = (SecurityTokenParameters)elementToBeCloned._protectionTokenParameters.Clone();
            }

            _requireSignatureConfirmation = elementToBeCloned._requireSignatureConfirmation;
        }

        public SymmetricSecurityBindingElement()
            : this((SecurityTokenParameters)null)
        {
            // empty
        }

        public SymmetricSecurityBindingElement(SecurityTokenParameters protectionTokenParameters)
            : base()
        {
            _messageProtectionOrder = SecurityBindingElement.defaultMessageProtectionOrder;
            _requireSignatureConfirmation = SecurityBindingElement.defaultRequireSignatureConfirmation;
            _protectionTokenParameters = protectionTokenParameters;
        }

        public bool RequireSignatureConfirmation
        {
            get
            {
                return _requireSignatureConfirmation;
            }
            set
            {
                _requireSignatureConfirmation = value;
            }
        }

        public MessageProtectionOrder MessageProtectionOrder
        {
            get
            {
                return _messageProtectionOrder;
            }
            set
            {
                if (!MessageProtectionOrderHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _messageProtectionOrder = value;
            }
        }

        public SecurityTokenParameters ProtectionTokenParameters
        {
            get
            {
                return _protectionTokenParameters;
            }
            set
            {
                _protectionTokenParameters = value;
            }
        }

        internal override ISecurityCapabilities GetIndividualISecurityCapabilities()
        {
            bool supportsServerAuthentication = false;
            GetSupportingTokensCapabilities(out bool supportsClientAuthentication, out bool supportsClientWindowsIdentity);
            if (ProtectionTokenParameters != null)
            {
                supportsClientAuthentication = supportsClientAuthentication || ProtectionTokenParameters.SupportsClientAuthentication;
                supportsClientWindowsIdentity = supportsClientWindowsIdentity || ProtectionTokenParameters.SupportsClientWindowsIdentity;

                if (ProtectionTokenParameters.HasAsymmetricKey)
                {
                    supportsServerAuthentication = ProtectionTokenParameters.SupportsClientAuthentication;
                }
                else
                {
                    supportsServerAuthentication = ProtectionTokenParameters.SupportsServerAuthentication;
                }
            }

            return new SecurityCapabilities(supportsClientAuthentication, supportsServerAuthentication, supportsClientWindowsIdentity,
                ProtectionLevel.EncryptAndSign, ProtectionLevel.EncryptAndSign);
        }

        internal override bool SessionMode
        {
            get
            {
                SecureConversationSecurityTokenParameters secureConversationTokenParameters = ProtectionTokenParameters as SecureConversationSecurityTokenParameters;
                if (secureConversationTokenParameters != null)
                {
                    return secureConversationTokenParameters.RequireCancellation;
                }
                else
                {
                    return false;
                }
            }
        }

        internal override bool SupportsDuplex
        {
            get { return SessionMode; }
        }

        internal override bool SupportsRequestReply
        {
            get { return true; }
        }

        public override void SetKeyDerivation(bool requireDerivedKeys)
        {
            base.SetKeyDerivation(requireDerivedKeys);
            if (_protectionTokenParameters != null)
            {
                _protectionTokenParameters.RequireDerivedKeys = requireDerivedKeys;
            }
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(ChannelProtectionRequirements))
            {
                AddressingVersion addressing = MessageVersion.Default.Addressing;
                MessageEncodingBindingElement encoding = context.Binding.Elements.Find<MessageEncodingBindingElement>();
                if (encoding != null)
                {
                    addressing = encoding.MessageVersion.Addressing;
                }
                ChannelProtectionRequirements myRequirements = base.GetProtectionRequirements(addressing, ProtectionLevel.EncryptAndSign);
                myRequirements.Add(context.GetInnerProperty<ChannelProtectionRequirements>() ?? new ChannelProtectionRequirements());
                return (T)(object)myRequirements;
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(base.ToString());

            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "MessageProtectionOrder: {0}", _messageProtectionOrder.ToString()));
            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "RequireSignatureConfirmation: {0}", _requireSignatureConfirmation.ToString()));
            sb.Append("ProtectionTokenParameters: ");
            if (_protectionTokenParameters != null)
            {
                sb.AppendLine(_protectionTokenParameters.ToString().Trim().Replace("\n", "\n  "));
            }
            else
            {
                sb.AppendLine("null");
            }

            return sb.ToString().Trim();
        }

        public override BindingElement Clone()
        {
            return new SymmetricSecurityBindingElement(this);
        }

        internal override SecurityProtocolFactory CreateSecurityProtocolFactory<TChannel>(BindingContext context, SecurityCredentialsManager credentialsManager, bool isForService, BindingContext issuanceBindingContext)
        {
            throw new NotImplementedException();
        }

        protected override IServiceDispatcher BuildServiceDispatcherCore<TChannel>(BindingContext context, IServiceDispatcher serviceDispatcher)
        {
            throw new NotImplementedException();
        }
    }
}
