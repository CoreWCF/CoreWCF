// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal class CompositeDispatchFormatter : IDispatchMessageFormatter
    {
        private readonly IDispatchMessageFormatter _reply;
        private readonly IDispatchMessageFormatter _request;

        public CompositeDispatchFormatter(IDispatchMessageFormatter request, IDispatchMessageFormatter reply)
        {
            _request = request;
            _reply = reply;
        }

        public void DeserializeRequest(Message message, object[] parameters) => _request.DeserializeRequest(message, parameters);

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result) => _reply.SerializeReply(messageVersion, parameters, result);
    }
}
