// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Runtime;
using CoreWCF.Security;
using System.Xml;

namespace CoreWCF.Channels
{
    internal abstract class WsrmIndex
    {
        private static WsrmFeb2005Index s_wsAddressingAug2004WSReliableMessagingFeb2005;
        private static WsrmFeb2005Index s_wsAddressing10WSReliableMessagingFeb2005;
        private static Wsrm11Index s_wsAddressingAug2004WSReliableMessaging11;
        private static Wsrm11Index s_wsAddressing10WSReliableMessaging11;

        internal static ActionHeader GetAckRequestedActionHeader(AddressingVersion addressingVersion,
            ReliableMessagingVersion reliableMessagingVersion)
        {
            return GetActionHeader(addressingVersion, reliableMessagingVersion, WsrmFeb2005Strings.AckRequested);
        }

        protected abstract ActionHeader GetActionHeader(string element);

        private static ActionHeader GetActionHeader(AddressingVersion addressingVersion,
            ReliableMessagingVersion reliableMessagingVersion, string element)
        {
            WsrmIndex cache = null;

            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                if (addressingVersion == AddressingVersion.WSAddressingAugust2004)
                {
                    if (s_wsAddressingAug2004WSReliableMessagingFeb2005 == null)
                    {
                        s_wsAddressingAug2004WSReliableMessagingFeb2005 = new WsrmFeb2005Index(addressingVersion);
                    }

                    cache = s_wsAddressingAug2004WSReliableMessagingFeb2005;
                }
                else if (addressingVersion == AddressingVersion.WSAddressing10)
                {
                    if (s_wsAddressing10WSReliableMessagingFeb2005 == null)
                    {
                        s_wsAddressing10WSReliableMessagingFeb2005 = new WsrmFeb2005Index(addressingVersion);
                    }

                    cache = s_wsAddressing10WSReliableMessagingFeb2005;
                }
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                if (addressingVersion == AddressingVersion.WSAddressingAugust2004)
                {
                    if (s_wsAddressingAug2004WSReliableMessaging11 == null)
                    {
                        s_wsAddressingAug2004WSReliableMessaging11 = new Wsrm11Index(addressingVersion);
                    }

                    cache = s_wsAddressingAug2004WSReliableMessaging11;
                }
                else if (addressingVersion == AddressingVersion.WSAddressing10)
                {
                    if (s_wsAddressing10WSReliableMessaging11 == null)
                    {
                        s_wsAddressing10WSReliableMessaging11 = new Wsrm11Index(addressingVersion);
                    }

                    cache = s_wsAddressing10WSReliableMessaging11;
                }
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }

            if (cache == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, addressingVersion)));
            }

            return cache.GetActionHeader(element);
        }

        internal static ActionHeader GetCloseSequenceActionHeader(AddressingVersion addressingVersion)
        {
            return GetActionHeader(addressingVersion, ReliableMessagingVersion.WSReliableMessaging11, Wsrm11Strings.CloseSequence);
        }

        internal static ActionHeader GetCloseSequenceResponseActionHeader(AddressingVersion addressingVersion)
        {
            return GetActionHeader(addressingVersion, ReliableMessagingVersion.WSReliableMessaging11, Wsrm11Strings.CloseSequenceResponse);
        }

        internal static ActionHeader GetCreateSequenceActionHeader(AddressingVersion addressingVersion,
            ReliableMessagingVersion reliableMessagingVersion)
        {
            return GetActionHeader(addressingVersion, reliableMessagingVersion, WsrmFeb2005Strings.CreateSequence);
        }

        internal static string GetCreateSequenceActionString(ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                return WsrmFeb2005Strings.CreateSequenceAction;
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return Wsrm11Strings.CreateSequenceAction;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static XmlDictionaryString GetCreateSequenceResponseAction(ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                return XD.WsrmFeb2005Dictionary.CreateSequenceResponseAction;
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return DXD.Wsrm11Dictionary.CreateSequenceResponseAction;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static string GetCreateSequenceResponseActionString(ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                return WsrmFeb2005Strings.CreateSequenceResponseAction;
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return Wsrm11Strings.CreateSequenceResponseAction;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static string GetFaultActionString(AddressingVersion addressingVersion,
            ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                return addressingVersion.DefaultFaultAction;
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return Wsrm11Strings.FaultAction;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static XmlDictionaryString GetNamespace(ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                return XD.WsrmFeb2005Dictionary.Namespace;
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return DXD.Wsrm11Dictionary.Namespace;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static string GetNamespaceString(ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                return WsrmFeb2005Strings.Namespace;
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return Wsrm11Strings.Namespace;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static ActionHeader GetSequenceAcknowledgementActionHeader(AddressingVersion addressingVersion,
            ReliableMessagingVersion reliableMessagingVersion)
        {
            return GetActionHeader(addressingVersion, reliableMessagingVersion, WsrmFeb2005Strings.SequenceAcknowledgement);
        }

        internal static string GetSequenceAcknowledgementActionString(ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                return WsrmFeb2005Strings.SequenceAcknowledgementAction;
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return Wsrm11Strings.SequenceAcknowledgementAction;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static MessagePartSpecification GetSignedReliabilityMessageParts(
            ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                return WsrmFeb2005Index.SignedReliabilityMessageParts;
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return Wsrm11Index.SignedReliabilityMessageParts;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static ActionHeader GetTerminateSequenceActionHeader(AddressingVersion addressingVersion,
            ReliableMessagingVersion reliableMessagingVersion)
        {
            return GetActionHeader(addressingVersion, reliableMessagingVersion, WsrmFeb2005Strings.TerminateSequence);
        }

        internal static string GetTerminateSequenceActionString(ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                return WsrmFeb2005Strings.TerminateSequenceAction;
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return Wsrm11Strings.TerminateSequenceAction;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static string GetTerminateSequenceResponseActionString(ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                return Wsrm11Strings.TerminateSequenceResponseAction;
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        internal static ActionHeader GetTerminateSequenceResponseActionHeader(AddressingVersion addressingVersion)
        {
            return GetActionHeader(addressingVersion, ReliableMessagingVersion.WSReliableMessaging11,
                Wsrm11Strings.TerminateSequenceResponse);
        }
    }

    internal class Wsrm11Index : WsrmIndex
    {
        private static MessagePartSpecification s_signedReliabilityMessageParts;
        private ActionHeader _ackRequestedActionHeader;
        private readonly AddressingVersion _addressingVersion;
        private ActionHeader _closeSequenceActionHeader;
        private ActionHeader _closeSequenceResponseActionHeader;
        private ActionHeader _createSequenceActionHeader;
        private ActionHeader _sequenceAcknowledgementActionHeader;
        private ActionHeader _terminateSequenceActionHeader;
        private ActionHeader _terminateSequenceResponseActionHeader;

        internal Wsrm11Index(AddressingVersion addressingVersion)
        {
            _addressingVersion = addressingVersion;
        }

        internal static MessagePartSpecification SignedReliabilityMessageParts
        {
            get
            {
                if (s_signedReliabilityMessageParts == null)
                {
                    XmlQualifiedName[] wsrmMessageHeaders = new XmlQualifiedName[]
                    {
                        new XmlQualifiedName(WsrmFeb2005Strings.Sequence, Wsrm11Strings.Namespace),
                        new XmlQualifiedName(WsrmFeb2005Strings.SequenceAcknowledgement, Wsrm11Strings.Namespace),
                        new XmlQualifiedName(WsrmFeb2005Strings.AckRequested, Wsrm11Strings.Namespace),
                        new XmlQualifiedName(Wsrm11Strings.UsesSequenceSTR, Wsrm11Strings.Namespace),
                    };

                    MessagePartSpecification s = new MessagePartSpecification(wsrmMessageHeaders);
                    s.MakeReadOnly();
                    s_signedReliabilityMessageParts = s;
                }

                return s_signedReliabilityMessageParts;
            }
        }

        protected override ActionHeader GetActionHeader(string element)
        {
            Wsrm11Dictionary wsrm11Dictionary = DXD.Wsrm11Dictionary;
            if (element == WsrmFeb2005Strings.AckRequested)
            {
                if (_ackRequestedActionHeader == null)
                {
                    _ackRequestedActionHeader = ActionHeader.Create(wsrm11Dictionary.AckRequestedAction,
                        _addressingVersion);
                }

                return _ackRequestedActionHeader;
            }
            else if (element == WsrmFeb2005Strings.CreateSequence)
            {
                if (_createSequenceActionHeader == null)
                {
                    _createSequenceActionHeader = ActionHeader.Create(wsrm11Dictionary.CreateSequenceAction,
                        _addressingVersion);
                }

                return _createSequenceActionHeader;
            }
            else if (element == WsrmFeb2005Strings.SequenceAcknowledgement)
            {
                if (_sequenceAcknowledgementActionHeader == null)
                {
                    _sequenceAcknowledgementActionHeader =
                        ActionHeader.Create(wsrm11Dictionary.SequenceAcknowledgementAction,
                        _addressingVersion);
                }

                return _sequenceAcknowledgementActionHeader;
            }
            else if (element == WsrmFeb2005Strings.TerminateSequence)
            {
                if (_terminateSequenceActionHeader == null)
                {
                    _terminateSequenceActionHeader =
                        ActionHeader.Create(wsrm11Dictionary.TerminateSequenceAction, _addressingVersion);
                }

                return _terminateSequenceActionHeader;
            }
            else if (element == Wsrm11Strings.TerminateSequenceResponse)
            {
                if (_terminateSequenceResponseActionHeader == null)
                {
                    _terminateSequenceResponseActionHeader =
                        ActionHeader.Create(wsrm11Dictionary.TerminateSequenceResponseAction, _addressingVersion);
                }

                return _terminateSequenceResponseActionHeader;
            }
            else if (element == Wsrm11Strings.CloseSequence)
            {
                if (_closeSequenceActionHeader == null)
                {
                    _closeSequenceActionHeader =
                        ActionHeader.Create(wsrm11Dictionary.CloseSequenceAction, _addressingVersion);
                }

                return _closeSequenceActionHeader;
            }
            else if (element == Wsrm11Strings.CloseSequenceResponse)
            {
                if (_closeSequenceResponseActionHeader == null)
                {
                    _closeSequenceResponseActionHeader =
                        ActionHeader.Create(wsrm11Dictionary.CloseSequenceResponseAction, _addressingVersion);
                }

                return _closeSequenceResponseActionHeader;
            }
            else
            {
                throw Fx.AssertAndThrow("Element not supported.");
            }
        }
    }

    internal class WsrmFeb2005Index : WsrmIndex
    {
        private static MessagePartSpecification s_signedReliabilityMessageParts;
        private ActionHeader _ackRequestedActionHeader;
        private readonly AddressingVersion _addressingVersion;
        private ActionHeader _createSequenceActionHeader;
        private ActionHeader _sequenceAcknowledgementActionHeader;
        private ActionHeader _terminateSequenceActionHeader;

        internal WsrmFeb2005Index(AddressingVersion addressingVersion)
        {
            _addressingVersion = addressingVersion;
        }

        internal static MessagePartSpecification SignedReliabilityMessageParts
        {
            get
            {
                if (s_signedReliabilityMessageParts == null)
                {
                    XmlQualifiedName[] wsrmMessageHeaders = new XmlQualifiedName[]
                    {
                        new XmlQualifiedName(WsrmFeb2005Strings.Sequence, WsrmFeb2005Strings.Namespace),
                        new XmlQualifiedName(WsrmFeb2005Strings.SequenceAcknowledgement, WsrmFeb2005Strings.Namespace),
                        new XmlQualifiedName(WsrmFeb2005Strings.AckRequested, WsrmFeb2005Strings.Namespace),
                    };

                    MessagePartSpecification s = new MessagePartSpecification(wsrmMessageHeaders);
                    s.MakeReadOnly();
                    s_signedReliabilityMessageParts = s;
                }

                return s_signedReliabilityMessageParts;
            }
        }

        protected override ActionHeader GetActionHeader(string element)
        {
            WsrmFeb2005Dictionary wsrmFeb2005Dictionary = XD.WsrmFeb2005Dictionary;

            if (element == WsrmFeb2005Strings.AckRequested)
            {
                if (_ackRequestedActionHeader == null)
                {
                    _ackRequestedActionHeader = ActionHeader.Create(wsrmFeb2005Dictionary.AckRequestedAction,
                        _addressingVersion);
                }

                return _ackRequestedActionHeader;
            }
            else if (element == WsrmFeb2005Strings.CreateSequence)
            {
                if (_createSequenceActionHeader == null)
                {
                    _createSequenceActionHeader =
                        ActionHeader.Create(wsrmFeb2005Dictionary.CreateSequenceAction, _addressingVersion);
                }

                return _createSequenceActionHeader;
            }
            else if (element == WsrmFeb2005Strings.SequenceAcknowledgement)
            {
                if (_sequenceAcknowledgementActionHeader == null)
                {
                    _sequenceAcknowledgementActionHeader =
                        ActionHeader.Create(wsrmFeb2005Dictionary.SequenceAcknowledgementAction,
                        _addressingVersion);
                }

                return _sequenceAcknowledgementActionHeader;
            }
            else if (element == WsrmFeb2005Strings.TerminateSequence)
            {
                if (_terminateSequenceActionHeader == null)
                {
                    _terminateSequenceActionHeader =
                        ActionHeader.Create(wsrmFeb2005Dictionary.TerminateSequenceAction, _addressingVersion);
                }

                return _terminateSequenceActionHeader;
            }
            else
            {
                throw Fx.AssertAndThrow("Element not supported.");
            }
        }
    }
}
