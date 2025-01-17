// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Queue.Common;

namespace CoreWCF.Queue.Tests.Fakes
{
    internal class FakeQueueTransport : IQueueTransport
    {
        private int _callCount;
        public int CallCount => _callCount;
        public int ConcurrencyLevel => 1;
        private readonly CallType _callType;

        public FakeQueueTransport(CallType callType)
        {
            _callType = callType;
        }

        public async ValueTask<QueueMessageContext> ReceiveQueueMessageContextAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken);
            Interlocked.Increment(ref _callCount);

            if (_callType == CallType.ThrowException)
                throw new OperationCanceledException();

            if (_callType == CallType.ReturnNull)
                return null;

            return new QueueMessageContext();
        }
    }

    internal enum CallType
    {
        Success,
        ReturnNull,
        ThrowException
    }
}
