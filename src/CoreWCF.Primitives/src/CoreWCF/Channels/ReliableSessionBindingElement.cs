// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.Security;
using System.Xml;
using CoreWCF.Runtime;
using System;
using CoreWCF.Configuration;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Channels
{
    public sealed class ReliableSessionBindingElement : BindingElement, IPolicyExportExtension
    {
        private TimeSpan _acknowledgementInterval = ReliableSessionDefaults.AcknowledgementInterval;
        private bool _flowControlEnabled = ReliableSessionDefaults.FlowControlEnabled;
        private TimeSpan _inactivityTimeout = ReliableSessionDefaults.InactivityTimeout;
        private int _maxPendingChannels = ReliableSessionDefaults.MaxPendingChannels;
        private int _maxRetryCount = ReliableSessionDefaults.MaxRetryCount;
        private int maxTransferWindowSize = ReliableSessionDefaults.MaxTransferWindowSize;
        private ReliableMessagingVersion _reliableMessagingVersion = ReliableMessagingVersion.Default;
        //private InternalDuplexBindingElement _internalDuplexBindingElement; // Needed if we bring back CompositeDuplexBindingElement
        private static MessagePartSpecification s_bodyOnly;

        public ReliableSessionBindingElement()
        {
        }

        internal ReliableSessionBindingElement(ReliableSessionBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            AcknowledgementInterval = elementToBeCloned.AcknowledgementInterval;
            FlowControlEnabled = elementToBeCloned.FlowControlEnabled;
            InactivityTimeout = elementToBeCloned.InactivityTimeout;
            MaxPendingChannels = elementToBeCloned.MaxPendingChannels;
            MaxRetryCount = elementToBeCloned.MaxRetryCount;
            MaxTransferWindowSize = elementToBeCloned.MaxTransferWindowSize;
            Ordered = elementToBeCloned.Ordered;
            ReliableMessagingVersion = elementToBeCloned.ReliableMessagingVersion;
        }

        public ReliableSessionBindingElement(bool ordered)
        {
            Ordered = ordered;
        }

        [DefaultValue(typeof(TimeSpan), ReliableSessionDefaults.AcknowledgementIntervalString)]
        public TimeSpan AcknowledgementInterval
        {
            get
            {
                return _acknowledgementInterval;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.SFxTimeoutOutOfRangeTooBig));
                }

                _acknowledgementInterval = value;
            }
        }

        [DefaultValue(ReliableSessionDefaults.FlowControlEnabled)]
        public bool FlowControlEnabled
        {
            get
            {
                return _flowControlEnabled;
            }
            set
            {
                _flowControlEnabled = value;
            }
        }

        [DefaultValue(typeof(TimeSpan), ReliableSessionDefaults.InactivityTimeoutString)]
        public TimeSpan InactivityTimeout
        {
            get
            {
                return _inactivityTimeout;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.TimeSpanMustBeGreaterThanTimeSpanZero));
                }

                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.SFxTimeoutOutOfRangeTooBig));
                }

                _inactivityTimeout = value;
            }
        }

        [DefaultValue(ReliableSessionDefaults.MaxPendingChannels)]
        public int MaxPendingChannels
        {
            get
            {
                return _maxPendingChannels;
            }
            set
            {
                if (value <= 0 || value > 16384)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SR.ValueMustBeInRange, 0, 16384)));
                _maxPendingChannels = value;
            }
        }

        [DefaultValue(ReliableSessionDefaults.MaxRetryCount)]
        public int MaxRetryCount
        {
            get
            {
                return _maxRetryCount;
            }
            set
            {
                if (value <= 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SRCommon.ValueMustBePositive));
                _maxRetryCount = value;
            }
        }

        [DefaultValue(ReliableSessionDefaults.MaxTransferWindowSize)]
        public int MaxTransferWindowSize
        {
            get
            {
                return maxTransferWindowSize;
            }
            set
            {
                if (value <= 0 || value > 4096)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                                                    SR.Format(SR.ValueMustBeInRange, 0, 4096)));
                maxTransferWindowSize = value;
            }
        }

        [DefaultValue(ReliableSessionDefaults.Ordered)]
        public bool Ordered { get; set; } = ReliableSessionDefaults.Ordered;

        [DefaultValue(typeof(ReliableMessagingVersion), ReliableSessionDefaults.ReliableMessagingVersionString)]
        public ReliableMessagingVersion ReliableMessagingVersion
        {
            get
            {
                return _reliableMessagingVersion;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (!ReliableMessagingVersion.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _reliableMessagingVersion = value;
            }
        }

        private static MessagePartSpecification BodyOnly
        {
            get
            {
                if (s_bodyOnly == null)
                {
                    MessagePartSpecification temp = new MessagePartSpecification(true);
                    temp.MakeReadOnly();
                    s_bodyOnly = temp;
                }

                return s_bodyOnly;
            }
        }

        public override BindingElement Clone()
        {
            return new ReliableSessionBindingElement(this);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }
            if (typeof(T) == typeof(ChannelProtectionRequirements))
            {
                ChannelProtectionRequirements myRequirements = GetProtectionRequirements();
                myRequirements.Add(context.GetInnerProperty<ChannelProtectionRequirements>() ?? new ChannelProtectionRequirements());
                return (T)(object)myRequirements;
            }
            else if (typeof(T) == typeof(IBindingDeliveryCapabilities))
            {
                return (T)(object)new BindingDeliveryCapabilitiesHelper(this, context.GetInnerProperty<IBindingDeliveryCapabilities>());
            }
            else
            {
                return context.GetInnerProperty<T>();
            }
        }

        private ChannelProtectionRequirements GetProtectionRequirements()
        {
            // Listing headers that must be signed.
            ChannelProtectionRequirements result = new ChannelProtectionRequirements();
            MessagePartSpecification signedReliabilityMessageParts = WsrmIndex.GetSignedReliabilityMessageParts(
                _reliableMessagingVersion);
            result.IncomingSignatureParts.AddParts(signedReliabilityMessageParts);
            result.OutgoingSignatureParts.AddParts(signedReliabilityMessageParts);

            if (_reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                // Adding RM protocol message actions so that each RM protocol message's body will be 
                // signed and encrypted.
                // From the Client to the Service
                ScopedMessagePartSpecification signaturePart = result.IncomingSignatureParts;
                ScopedMessagePartSpecification encryptionPart = result.IncomingEncryptionParts;
                ProtectProtocolMessage(signaturePart, encryptionPart, WsrmFeb2005Strings.AckRequestedAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, WsrmFeb2005Strings.CreateSequenceAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, WsrmFeb2005Strings.SequenceAcknowledgementAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, WsrmFeb2005Strings.LastMessageAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, WsrmFeb2005Strings.TerminateSequenceAction);

                // From the Service to the Client
                signaturePart = result.OutgoingSignatureParts;
                encryptionPart = result.OutgoingEncryptionParts;
                ProtectProtocolMessage(signaturePart, encryptionPart, WsrmFeb2005Strings.CreateSequenceResponseAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, WsrmFeb2005Strings.SequenceAcknowledgementAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, WsrmFeb2005Strings.LastMessageAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, WsrmFeb2005Strings.TerminateSequenceAction);
            }
            else if (_reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                // Adding RM protocol message actions so that each RM protocol message's body will be 
                // signed and encrypted.
                // From the Client to the Service
                ScopedMessagePartSpecification signaturePart = result.IncomingSignatureParts;
                ScopedMessagePartSpecification encryptionPart = result.IncomingEncryptionParts;
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.AckRequestedAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.CloseSequenceAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.CloseSequenceResponseAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.CreateSequenceAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.FaultAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.SequenceAcknowledgementAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.TerminateSequenceAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.TerminateSequenceResponseAction);

                // From the Service to the Client
                signaturePart = result.OutgoingSignatureParts;
                encryptionPart = result.OutgoingEncryptionParts;
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.AckRequestedAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.CloseSequenceAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.CloseSequenceResponseAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.CreateSequenceResponseAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.FaultAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.SequenceAcknowledgementAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.TerminateSequenceAction);
                ProtectProtocolMessage(signaturePart, encryptionPart, Wsrm11Strings.TerminateSequenceResponseAction);
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }

            return result;
        }

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher)
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));

            VerifyTransportMode(context);
            SetSecuritySettings(context);

            IMessageFilterTable<EndpointAddress> table = context.BindingParameters.Find<IMessageFilterTable<EndpointAddress>>();

            // I believe this is only a theoretical possibility so not providing an informative
            // exception message. This would require transports which we don't support such as
            // UdpBinding.
            if (InternalDuplexBindingElement.RequiresDuplexBinding(context))
            {
                throw new PlatformNotSupportedException();
            }

            if (typeof(TChannel) == typeof(IInputSessionChannel))
            {
                // Only IInputSessionChannel implementation inbox for WCF is Msmq and that doesn't make a lot of sense
                // to use reliable sessions with. Queues are inherently designed to be reliable (can replay uncompleted
                // messages, offline reliable storage of message etc) and aren't intended to use real time responses
                // (although you can if you really want). ReliableSessions is based on getting real time responses.
                // If there's ever a reason for needing this, we can port these types.
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(TChannel), SR.Format(SR.ChannelTypeNotSupported, typeof(TChannel)));
            }
            else if (typeof(TChannel) == typeof(IDuplexSessionChannel))
            {
                ReliableServiceDispatcherBase<IDuplexSessionChannel> serviceDispatcher = null;

                if (context.CanBuildNextServiceDispatcher<IDuplexSessionChannel>())
                {
                    throw new PlatformNotSupportedException();
                    //serviceDispatcher = new ReliableDuplexServiceDispatcherOverDuplexSession(this, context);
                }
                else if (context.CanBuildNextServiceDispatcher<IDuplexChannel>())
                {
                    throw new PlatformNotSupportedException();
                    //serviceDispatcher = new ReliableDuplexServiceDispatcherOverDuplex(this, innerDispatcher);
                }

                if (serviceDispatcher != null)
                {
                    serviceDispatcher.LocalAddresses = table;
                    return serviceDispatcher;
                }
            }
            else if (typeof(TChannel) == typeof(IReplySessionChannel))
            {
                ReliableServiceDispatcherBase<IReplySessionChannel> serviceDispatcher = null;

                if (context.CanBuildNextServiceDispatcher<IReplySessionChannel>())
                {
                    serviceDispatcher = new ReliableReplyServiceDispatcherOverReplySession(this, context, innerDispatcher);
                }
                else if (context.CanBuildNextServiceDispatcher<IReplyChannel>())
                {
                    serviceDispatcher = new ReliableReplyServiceDispatcherOverReply(this, context, innerDispatcher);
                }

                if (serviceDispatcher != null)
                {
                    serviceDispatcher.LocalAddresses = table;
                    return serviceDispatcher;
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(TChannel), SR.Format(SR.ChannelTypeNotSupported, typeof(TChannel)));
        }

        public override bool CanBuildServiceDispatcher<TChannel>(BindingContext context)
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));

            // I believe this is only a theoretical possibility so not providing an informative
            // exception message. This would require transports which we don't support such as
            // UdpBinding.
            if (InternalDuplexBindingElement.RequiresDuplexBinding(context))
            {
                return false;
            }

            if (typeof(TChannel) == typeof(IInputSessionChannel))
            {
                return context.CanBuildNextServiceDispatcher<IReplySessionChannel>()
                    || context.CanBuildNextServiceDispatcher<IReplyChannel>()
                    || context.CanBuildNextServiceDispatcher<IDuplexSessionChannel>()
                    || context.CanBuildNextServiceDispatcher<IDuplexChannel>();
            }
            else if (typeof(TChannel) == typeof(IDuplexSessionChannel))
            {
                return context.CanBuildNextServiceDispatcher<IDuplexSessionChannel>()
                    || context.CanBuildNextServiceDispatcher<IDuplexChannel>();
            }
            else if (typeof(TChannel) == typeof(IReplySessionChannel))
            {
                return context.CanBuildNextServiceDispatcher<IReplySessionChannel>()
                    || context.CanBuildNextServiceDispatcher<IReplyChannel>();
            }

            return false;
        }


        private static void ProtectProtocolMessage(
            ScopedMessagePartSpecification signaturePart,
            ScopedMessagePartSpecification encryptionPart,
            string action)
        {
            signaturePart.AddParts(BodyOnly, action);
            encryptionPart.AddParts(MessagePartSpecification.NoParts, action);
        }

        private void SetSecuritySettings(BindingContext context)
        {
            SecurityBindingElement element = context.RemainingBindingElements.Find<SecurityBindingElement>();

            if (element != null)
            {
                element.LocalServiceSettings.ReconnectTransportOnFailure = true;
            }
        }

        private void VerifyTransportMode(BindingContext context)
        {
            TransportBindingElement transportElement = context.RemainingBindingElements.Find<TransportBindingElement>();

            // Verify ManualAdderssing is turned off.
            if ((transportElement != null) && (transportElement.ManualAddressing))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.ManualAddressingNotSupported));
            }

            // Verify TransportMode is Buffered.
            TransferMode transportTransferMode;
            Tuple<TransferMode> transportTransferModeHolder = transportElement.GetProperty<Tuple<TransferMode>>(context);

            if (transportTransferModeHolder != null)
            {
                transportTransferMode = transportTransferModeHolder.Item1;
            }
            else
            {
                // Not one of the elements we can check, we have to assume TransferMode.Buffered.
                transportTransferMode = TransferMode.Buffered;
            }

            if (transportTransferMode != TransferMode.Buffered)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                new InvalidOperationException(SR.Format(SR.TransferModeNotSupported,
                    transportTransferMode, GetType().Name)));
            }
        }

        void IPolicyExportExtension.ExportPolicy(MetadataExporter exporter, PolicyConversionContext context)
        {
            if (exporter == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));

            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));

            if (context.BindingElements != null)
            {
                BindingElementCollection bindingElements = context.BindingElements;
                ReliableSessionBindingElement settings = bindingElements.Find<ReliableSessionBindingElement>();

                if (settings != null)
                {
                    // ReliableSession assertion
                    XmlElement assertion = settings.CreateReliabilityAssertion(exporter.PolicyVersion, bindingElements);
                    context.GetBindingAssertions().Add(assertion);
                }
            }
        }

        private static XmlElement CreatePolicyElement(PolicyVersion policyVersion, XmlDocument doc)
        {
            string policy = MetadataStrings.WSPolicy.Elements.Policy;
            string policyNs = policyVersion.Namespace;
            string policyPrefix = MetadataStrings.WSPolicy.Prefix;

            return doc.CreateElement(policyPrefix, policy, policyNs);
        }

        private XmlElement CreateReliabilityAssertion(PolicyVersion policyVersion, BindingElementCollection bindingElements)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement child = null;
            string reliableSessionPrefix;
            string reliableSessionNs;
            string assertionPrefix;
            string assertionNs;

            if (ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                reliableSessionPrefix = ReliableSessionPolicyStrings.ReliableSessionFebruary2005Prefix;
                reliableSessionNs = ReliableSessionPolicyStrings.ReliableSessionFebruary2005Namespace;
                assertionPrefix = reliableSessionPrefix;
                assertionNs = reliableSessionNs;
            }
            else
            {
                reliableSessionPrefix = ReliableSessionPolicyStrings.ReliableSession11Prefix;
                reliableSessionNs = ReliableSessionPolicyStrings.ReliableSession11Namespace;
                assertionPrefix = ReliableSessionPolicyStrings.NET11Prefix;
                assertionNs = ReliableSessionPolicyStrings.NET11Namespace;
            }

            // ReliableSession assertion
            XmlElement assertion = doc.CreateElement(reliableSessionPrefix, ReliableSessionPolicyStrings.ReliableSessionName, reliableSessionNs);

            if (ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                // Policy
                XmlElement policy = CreatePolicyElement(policyVersion, doc);

                // SequenceSTR
                if (IsSecureConversationEnabled(bindingElements))
                {
                    XmlElement sequenceSTR = doc.CreateElement(reliableSessionPrefix, ReliableSessionPolicyStrings.SequenceSTR, reliableSessionNs);
                    policy.AppendChild(sequenceSTR);
                }

                // DeliveryAssurance
                XmlElement deliveryAssurance = doc.CreateElement(reliableSessionPrefix, ReliableSessionPolicyStrings.DeliveryAssurance, reliableSessionNs);

                // Policy
                XmlElement nestedPolicy = CreatePolicyElement(policyVersion, doc);

                // ExactlyOnce
                XmlElement exactlyOnce = doc.CreateElement(reliableSessionPrefix, ReliableSessionPolicyStrings.ExactlyOnce, reliableSessionNs);
                nestedPolicy.AppendChild(exactlyOnce);

                if (Ordered)
                {
                    // InOrder
                    XmlElement inOrder = doc.CreateElement(reliableSessionPrefix, ReliableSessionPolicyStrings.InOrder, reliableSessionNs);
                    nestedPolicy.AppendChild(inOrder);
                }

                deliveryAssurance.AppendChild(nestedPolicy);
                policy.AppendChild(deliveryAssurance);
                assertion.AppendChild(policy);
            }

            // Nested InactivityTimeout assertion
            child = doc.CreateElement(assertionPrefix, ReliableSessionPolicyStrings.InactivityTimeout, assertionNs);
            WriteMillisecondsAttribute(child, InactivityTimeout);
            assertion.AppendChild(child);

            // Nested AcknowledgementInterval assertion
            child = doc.CreateElement(assertionPrefix, ReliableSessionPolicyStrings.AcknowledgementInterval, assertionNs);
            WriteMillisecondsAttribute(child, AcknowledgementInterval);
            assertion.AppendChild(child);

            return assertion;
        }

        private static bool IsSecureConversationEnabled(BindingElementCollection bindingElements)
        {
            bool foundRM = false;

            for (int i = 0; i < bindingElements.Count; i++)
            {
                if (!foundRM)
                {
                    ReliableSessionBindingElement bindingElement = bindingElements[i] as ReliableSessionBindingElement;
                    foundRM = (bindingElement != null);
                }
                else
                {
                    SecurityBindingElement securityBindingElement = bindingElements[i] as SecurityBindingElement;

                    if (securityBindingElement != null)
                    {
                        return IsSecureConversationBinding(securityBindingElement);
                    }

                    break;
                }
            }

            return false;
        }

        private static bool IsSecureConversationBinding(SecurityBindingElement sbe)
        {
            SecurityBindingElement bootstrapSecurity = null;
            SymmetricSecurityBindingElement ssbe = sbe as SymmetricSecurityBindingElement;
            if (ssbe != null)
            {
                if (ssbe.RequireSignatureConfirmation)
                    return false;

                SecureConversationSecurityTokenParameters parameters = ssbe.ProtectionTokenParameters as SecureConversationSecurityTokenParameters;
                if (parameters == null)
                    return false;
                bootstrapSecurity = parameters.BootstrapSecurityBindingElement;
            }
            else
            {
                if (!sbe.IncludeTimestamp)
                    return false;

                // do not check local settings: sbe.LocalServiceSettings and sbe.LocalClientSettings

                if (!(sbe is TransportSecurityBindingElement))
                    return false;

                SupportingTokenParameters parameters = sbe.EndpointSupportingTokenParameters;
                if (parameters.Signed.Count != 0 || parameters.SignedEncrypted.Count != 0 || parameters.Endorsing.Count != 1 || parameters.SignedEndorsing.Count != 0)
                    return false;
                SecureConversationSecurityTokenParameters scParameters = parameters.Endorsing[0] as SecureConversationSecurityTokenParameters;
                if (scParameters == null)
                    return false;

                bootstrapSecurity = scParameters.BootstrapSecurityBindingElement;
            }

            if (bootstrapSecurity != null && bootstrapSecurity.SecurityHeaderLayout != SecurityProtocolFactory.defaultSecurityHeaderLayout)
                return false;

            return bootstrapSecurity != null;
        }

        private static void WriteMillisecondsAttribute(XmlElement childElement, TimeSpan timeSpan)
        {
            UInt64 milliseconds = Convert.ToUInt64(timeSpan.TotalMilliseconds);
            childElement.SetAttribute(ReliableSessionPolicyStrings.Milliseconds, XmlConvert.ToString(milliseconds));
        }

        private class BindingDeliveryCapabilitiesHelper : IBindingDeliveryCapabilities
        {
            private readonly ReliableSessionBindingElement _element;
            private readonly IBindingDeliveryCapabilities _inner;

            internal BindingDeliveryCapabilitiesHelper(ReliableSessionBindingElement element, IBindingDeliveryCapabilities inner)
            {
                _element = element;
                _inner = inner;
            }
            bool IBindingDeliveryCapabilities.AssuresOrderedDelivery => _element.Ordered;
            bool IBindingDeliveryCapabilities.QueuedDelivery => _inner != null ? _inner.QueuedDelivery : false;
        }
    }

}
