// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Helpers.Interceptor
{
    /// <summary>
    /// WS-ReliableMessaging SOAP action URIs. Hardcoded so the test interceptor can match
    /// outbound/inbound messages without depending on internals of CoreWCF.Primitives.
    /// </summary>
    internal static class WsrmActions
    {
        // WS-RM February 2005 (http://schemas.xmlsoap.org/ws/2005/02/rm)
        public const string Feb2005Namespace = "http://schemas.xmlsoap.org/ws/2005/02/rm";
        public const string Feb2005CreateSequence = Feb2005Namespace + "/CreateSequence";
        public const string Feb2005CreateSequenceResponse = Feb2005Namespace + "/CreateSequenceResponse";
        public const string Feb2005SequenceAcknowledgement = Feb2005Namespace + "/SequenceAcknowledgement";
        public const string Feb2005TerminateSequence = Feb2005Namespace + "/TerminateSequence";
        public const string Feb2005LastMessage = Feb2005Namespace + "/LastMessage";

        // WS-ReliableMessaging 1.1 (http://docs.oasis-open.org/ws-rx/wsrm/200702)
        public const string Wsrm11Namespace = "http://docs.oasis-open.org/ws-rx/wsrm/200702";
        public const string Wsrm11CreateSequence = Wsrm11Namespace + "/CreateSequence";
        public const string Wsrm11CreateSequenceResponse = Wsrm11Namespace + "/CreateSequenceResponse";
        public const string Wsrm11CloseSequence = Wsrm11Namespace + "/CloseSequence";
        public const string Wsrm11CloseSequenceResponse = Wsrm11Namespace + "/CloseSequenceResponse";
        public const string Wsrm11TerminateSequence = Wsrm11Namespace + "/TerminateSequence";
        public const string Wsrm11TerminateSequenceResponse = Wsrm11Namespace + "/TerminateSequenceResponse";
        public const string Wsrm11SequenceAcknowledgement = Wsrm11Namespace + "/SequenceAcknowledgement";
        public const string Wsrm11AckRequested = Wsrm11Namespace + "/AckRequested";
        public const string Wsrm11Fault = Wsrm11Namespace + "/fault";

        public static bool IsTerminateSequence(string action) =>
            action == Feb2005TerminateSequence || action == Wsrm11TerminateSequence;

        public static bool IsCloseSequence(string action) =>
            action == Wsrm11CloseSequence;

        public static bool IsCreateSequence(string action) =>
            action == Feb2005CreateSequence || action == Wsrm11CreateSequence;

        public static bool IsAcknowledgement(string action) =>
            action == Feb2005SequenceAcknowledgement || action == Wsrm11SequenceAcknowledgement;
    }
}
