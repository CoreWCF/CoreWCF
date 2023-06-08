// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Runtime;
using CoreWCF.Security;
using System.Xml;

namespace CoreWCF.Channels
{
    internal sealed class CreateSequence
    {
        public static CreateSequenceInfo Create(MessageVersion messageVersion,
            ReliableMessagingVersion reliableMessagingVersion, ISecureConversationSession securitySession,
            XmlDictionaryReader reader)
        {
            if (reader == null)
            {
                Fx.Assert("Argument reader cannot be null.");
            }

            try
            {
                CreateSequenceInfo info = new CreateSequenceInfo();
                WsrmFeb2005Dictionary wsrmFeb2005Dictionary = XD.WsrmFeb2005Dictionary;
                XmlDictionaryString wsrmNs = WsrmIndex.GetNamespace(reliableMessagingVersion);
                reader.ReadStartElement(wsrmFeb2005Dictionary.CreateSequence, wsrmNs);

                info.AcksTo = EndpointAddress.ReadFrom(messageVersion.Addressing, reader, wsrmFeb2005Dictionary.AcksTo, wsrmNs);

                if (reader.IsStartElement(wsrmFeb2005Dictionary.Expires, wsrmNs))
                {
                    info.Expires = reader.ReadElementContentAsTimeSpan();
                }

                if (reader.IsStartElement(wsrmFeb2005Dictionary.Offer, wsrmNs))
                {
                    reader.ReadStartElement();

                    reader.ReadStartElement(wsrmFeb2005Dictionary.Identifier, wsrmNs);
                    info.OfferIdentifier = reader.ReadContentAsUniqueId();
                    reader.ReadEndElement();

                    bool wsrm11 = reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11;
                    Wsrm11Dictionary wsrm11Dictionary = wsrm11 ? DXD.Wsrm11Dictionary : null;

                    if (wsrm11)
                    {
                        EndpointAddress endpoint = EndpointAddress.ReadFrom(messageVersion.Addressing, reader,
                            wsrm11Dictionary.Endpoint, wsrmNs);

                        if (endpoint.Uri != info.AcksTo.Uri)
                        {
                            string reason = SR.CSRefusedAcksToMustEqualEndpoint;
                            Message faultReply = WsrmUtilities.CreateCSRefusedProtocolFault(messageVersion, reliableMessagingVersion, reason);
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(WsrmMessageInfo.CreateInternalFaultException(faultReply, reason, new ProtocolException(reason)));
                        }
                    }

                    if (reader.IsStartElement(wsrmFeb2005Dictionary.Expires, wsrmNs))
                    {
                        info.OfferExpires = reader.ReadElementContentAsTimeSpan();
                    }

                    if (wsrm11)
                    {
                        if (reader.IsStartElement(wsrm11Dictionary.IncompleteSequenceBehavior, wsrmNs))
                        {
                            string incompleteSequenceBehavior = reader.ReadElementContentAsString();

                            if ((incompleteSequenceBehavior != Wsrm11Strings.DiscardEntireSequence)
                                && (incompleteSequenceBehavior != Wsrm11Strings.DiscardFollowingFirstGap)
                                && (incompleteSequenceBehavior != Wsrm11Strings.NoDiscard))
                            {
                                string reason = SR.CSRefusedInvalidIncompleteSequenceBehavior;
                                Message faultReply = WsrmUtilities.CreateCSRefusedProtocolFault(messageVersion, reliableMessagingVersion, reason);
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                    WsrmMessageInfo.CreateInternalFaultException(faultReply, reason,
                                    new ProtocolException(reason)));
                            }

                            // Otherwise ignore the value.
                        }
                    }

                    while (reader.IsStartElement())
                    {
                        reader.Skip();
                    }

                    reader.ReadEndElement();
                }

                // Check for security only if we expect a soap security session.
                if (securitySession != null)
                {
                    bool hasValidToken = false;

                    // Since the security element is amongst the extensible elements (i.e. there is no 
                    // gaurantee of ordering or placement), a loop is required to attempt to parse the 
                    // security element.
                    while (reader.IsStartElement())
                    {
                        if (securitySession.TryReadSessionTokenIdentifier(reader))
                        {
                            hasValidToken = true;
                            break;
                        }

                        reader.Skip();
                    }

                    if (!hasValidToken)
                    {
                        string reason = SR.CSRefusedRequiredSecurityElementMissing;
                        Message faultReply = WsrmUtilities.CreateCSRefusedProtocolFault(messageVersion, reliableMessagingVersion, reason);
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(WsrmMessageInfo.CreateInternalFaultException(faultReply, reason, new ProtocolException(reason)));
                    }
                }

                while (reader.IsStartElement())
                {
                    reader.Skip();
                }

                reader.ReadEndElement();

                if (reader.IsStartElement())
                {
                    string reason = SR.CSRefusedUnexpectedElementAtEndOfCSMessage;
                    Message faultReply = WsrmUtilities.CreateCSRefusedProtocolFault(messageVersion, reliableMessagingVersion, reason);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(WsrmMessageInfo.CreateInternalFaultException(faultReply, reason, new ProtocolException(reason)));
                }

                return info;
            }
            catch (XmlException e)
            {
                string reason = SR.Format(SR.CouldNotParseWithAction, WsrmIndex.GetCreateSequenceActionString(reliableMessagingVersion));
                Message faultReply = WsrmUtilities.CreateCSRefusedProtocolFault(messageVersion, reliableMessagingVersion, reason);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(WsrmMessageInfo.CreateInternalFaultException(faultReply, reason, new ProtocolException(reason, e)));
            }
        }
    }
}
