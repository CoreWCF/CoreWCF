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
        public int CallCount { get; private set; }
        public int ConcurrencyLevel => 1;
        private readonly CallType _callType;

        public FakeQueueTransport(CallType callType)
        {
            _callType = callType;
        }

        public async ValueTask<QueueMessageContext> ReceiveQueueMessageContextAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken);
            CallCount++;

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
