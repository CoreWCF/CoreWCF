// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel.Channels;

namespace Helpers.Interceptor
{
    /// <summary>
    /// Direction of a SOAP message at the interception point.
    /// </summary>
    internal enum InterceptDirection
    {
        Outbound,
        Inbound,
    }

    /// <summary>
    /// Disposition of an intercepted message: pass through, replace, or drop.
    /// </summary>
    public sealed class InterceptDecision
    {
        public static readonly InterceptDecision PassThrough = new InterceptDecision(null, false);
        public static readonly InterceptDecision Drop = new InterceptDecision(null, true);
        public static InterceptDecision Replace(Message replacement) => new InterceptDecision(replacement, false);

        private InterceptDecision(Message replacement, bool drop)
        {
            Replacement = replacement;
            Dropped = drop;
        }

        public Message Replacement { get; }
        public bool Dropped { get; }
    }

    /// <summary>
    /// Hooks invoked by <see cref="InterceptingDuplexSessionChannel"/> for each
    /// outbound and inbound SOAP message. Implementations can inspect, mutate, or drop
    /// messages to drive WS-RM protocol scenarios that are not reachable through the
    /// normal client API surface (e.g. sending CloseSequence without TerminateSequence,
    /// dropping an SequenceAcknowledgement, etc.).
    /// </summary>
    public interface IMessageInterceptor
    {
        /// <summary>
        /// Called for every outbound message just before it is handed to the inner channel.
        /// The interceptor is given the buffered message body so it can both inspect headers
        /// and decide whether to forward the message.
        /// </summary>
        InterceptDecision OnOutbound(Message message);

        /// <summary>
        /// Called for every inbound message just after it is read from the inner channel
        /// and before it is returned to the caller. Returning Drop causes the receive
        /// loop to immediately read the next message from the inner channel.
        /// </summary>
        InterceptDecision OnInbound(Message message);
    }
}
