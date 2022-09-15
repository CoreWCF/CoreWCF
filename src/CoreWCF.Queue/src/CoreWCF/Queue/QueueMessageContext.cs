// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF.Queue.Common
{
    public class QueueMessageContext : RequestContext
    {
        public PipeReader QueueMessageReader { get; set; }
        public IDictionary<string, object> Properties { get; set; }
        private Message _requestMessage;
        private Exception _requestMessageException;

        public override Message RequestMessage
        {
            get
            {
                if (_requestMessageException != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(_requestMessageException);
                }

                return _requestMessage;
            }
        }

        internal void SetRequestMessage(Message requestMessage)
        {
            Fx.Assert(_requestMessageException == null, "Cannot have both a requestMessage and a requestException.");
            _requestMessage = requestMessage;
        }

        internal void SetRequestMessage(Exception requestMessageException)
        {
            Fx.Assert(_requestMessage == null, "Cannot have both a requestMessage and a requestException.");
            _requestMessageException = requestMessageException;
        }

        public QueueTransportContext QueueTransportContext { get; set; }
        public EndpointAddress LocalAddress { get; set; }

        public override void Abort() => throw new System.NotImplementedException();

        public override Task ReplyAsync(Message message)
        {
           if (DispatchResultHandler != null)
            {
                if (message.IsFault) DispatchResultHandler(QueueDispatchResult.Failed, this);
                else DispatchResultHandler(QueueDispatchResult.Processed, this);
            }
            return Task.CompletedTask;
        }
        public override Task ReplyAsync(Message message, CancellationToken token) => throw new System.NotImplementedException();
        public override Task CloseAsync() => throw new System.NotImplementedException();
        public override Task CloseAsync(CancellationToken token) => throw new System.NotImplementedException();

        public Action<QueueDispatchResult, QueueMessageContext> DispatchResultHandler { get; set; }

    }

    public enum QueueDispatchResult
    {
        Processed,
        Failed,
        ABorted
    }
}
