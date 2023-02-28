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
        public virtual IDictionary<string, object> Properties { get { return _properties.Value; } }
        private Message _requestMessage;
        private Exception _requestMessageException;
        private ReceiveContext _receiveContext;
        private readonly Lazy<IDictionary<string, object>> _properties = new();

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

        public virtual ReceiveContext ReceiveContext
        {
            get
            {
                return _receiveContext;
            }
            set
            {
                _receiveContext = value;
                if (_requestMessage != null)
                {
                    // Attach _receiveContext to the message
                    SetRequestMessage(_requestMessage);
                }
            }
        }

        internal void SetRequestMessage(Message requestMessage)
        {
            Fx.Assert(_requestMessageException == null, "Cannot have both a requestMessage and a requestException.");
            
            if (_receiveContext != null)
            {
                requestMessage.Properties[ReceiveContext.Name] = _receiveContext;
            }

            _requestMessage = requestMessage;
        }

        internal void SetRequestMessage(Exception requestMessageException)
        {
            Fx.Assert(_requestMessage == null, "Cannot have both a requestMessage and a requestException.");
            _requestMessageException = requestMessageException;
        }

        public QueueTransportContext QueueTransportContext { get; set; }
        public EndpointAddress LocalAddress { get; set; }

        public override void Abort()
        {
            _requestMessage.Close();
        }

        public override async Task ReplyAsync(Message message)
        {
            if (message != null && message.Properties.TryGetValue(ReceiveContext.Name, out ReceiveContext receiveContext))
            {
                if (message.IsFault)
                {
                    await receiveContext.AbandonAsync(CancellationToken.None);
                }
                else
                {
                    await receiveContext.CompleteAsync(CancellationToken.None);
                }
            }
        }

        public override Task ReplyAsync(Message message, CancellationToken token)
        {
            return ReplyAsync(message);
        }

        public override Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        public override Task CloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
