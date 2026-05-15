// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel.Channels;
using System.Threading;

namespace Helpers.Interceptor
{
    /// <summary>
    /// Test interceptor that selectively drops outbound TerminateSequence messages so the
    /// remote peer observes a true WS-RM 1.1 half-close (CloseSequence only). The .NET
    /// Framework / CoreWCF client APIs send both CloseSequence and TerminateSequence as part
    /// of CloseOutputSession, so this interceptor is the only way to reach the half-close
    /// code path from a test.
    /// </summary>
    internal sealed class WsrmHalfCloseInterceptor : IMessageInterceptor
    {
        private int _suppressOutboundTerminateSequence;
        private int _suppressOutboundCloseSequence;
        private int _droppedOutboundTerminateSequenceCount;
        private int _droppedOutboundCloseSequenceCount;

        public bool SuppressOutboundTerminateSequence
        {
            get => Volatile.Read(ref _suppressOutboundTerminateSequence) != 0;
            set => Volatile.Write(ref _suppressOutboundTerminateSequence, value ? 1 : 0);
        }

        public bool SuppressOutboundCloseSequence
        {
            get => Volatile.Read(ref _suppressOutboundCloseSequence) != 0;
            set => Volatile.Write(ref _suppressOutboundCloseSequence, value ? 1 : 0);
        }

        public int DroppedOutboundTerminateSequenceCount => Volatile.Read(ref _droppedOutboundTerminateSequenceCount);
        public int DroppedOutboundCloseSequenceCount => Volatile.Read(ref _droppedOutboundCloseSequenceCount);

        public InterceptDecision OnOutbound(Message message)
        {
            string action = message.Headers.Action;

            if (SuppressOutboundTerminateSequence && WsrmActions.IsTerminateSequence(action))
            {
                Interlocked.Increment(ref _droppedOutboundTerminateSequenceCount);
                return InterceptDecision.Drop;
            }

            if (SuppressOutboundCloseSequence && WsrmActions.IsCloseSequence(action))
            {
                Interlocked.Increment(ref _droppedOutboundCloseSequenceCount);
                return InterceptDecision.Drop;
            }

            return InterceptDecision.PassThrough;
        }

        public InterceptDecision OnInbound(Message message) => InterceptDecision.PassThrough;
    }
}
