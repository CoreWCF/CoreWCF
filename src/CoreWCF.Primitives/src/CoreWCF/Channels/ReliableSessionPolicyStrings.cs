// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal static class ReliableSessionPolicyStrings
    {
        public const string AcknowledgementInterval = "AcknowledgementInterval";
        public const string AtLeastOnce = "AtLeastOnce";
        public const string AtMostOnce = "AtMostOnce";
        public const string BaseRetransmissionInterval = "BaseRetransmissionInterval";
        public const string DeliveryAssurance = "DeliveryAssurance";
        public const string ExactlyOnce = "ExactlyOnce";
        public const string ExponentialBackoff = "ExponentialBackoff";
        public const string InactivityTimeout = "InactivityTimeout";
        public const string InOrder = "InOrder";
        public const string Milliseconds = "Milliseconds";
        public const string NET11Namespace = "http://schemas.microsoft.com/ws-rx/wsrmp/200702";
        public const string NET11Prefix = "netrmp";
        public const string ReliableSessionName = "RMAssertion";
        public const string ReliableSessionFebruary2005Namespace = "http://schemas.xmlsoap.org/ws/2005/02/rm/policy";
        public const string ReliableSessionFebruary2005Prefix = "wsrm";
        public const string ReliableSession11Namespace = "http://docs.oasis-open.org/ws-rx/wsrmp/200702";
        public const string ReliableSession11Prefix = "wsrmp";
        public const string SequenceSTR = "SequenceSTR";
        public const string SequenceTransportSecurity = "SequenceTransportSecurity";
    }
}
