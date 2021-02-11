// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal struct ProxyRpc
    {
        internal readonly string Action;
        //internal ServiceModelActivity Activity;
        internal Guid ActivityId;
        internal readonly ServiceChannel Channel;
        internal object[] Correlation;
        internal readonly object[] InputParameters;
        internal readonly ProxyOperationRuntime Operation;
        internal object[] OutputParameters;
        internal Message Request;
        internal Message Reply;
        internal object ReturnValue;
        internal MessageVersion MessageVersion;
        //internal readonly TimeoutHelper TimeoutHelper;
        internal CancellationToken CancellationToken;
        //EventTraceActivity eventTraceActivity;

        internal ProxyRpc(ServiceChannel channel, ProxyOperationRuntime operation, string action, object[] inputs, CancellationToken token)
        {
            Action = action;
            //this.Activity = null;
            //this.eventTraceActivity = null;
            Channel = channel;
            Correlation = EmptyArray.Allocate(operation.Parent.CorrelationCount);
            InputParameters = inputs;
            Operation = operation;
            OutputParameters = null;
            Request = null;
            Reply = null;
            ActivityId = Guid.Empty;
            ReturnValue = null;
            MessageVersion = channel.MessageVersion;
            CancellationToken = token;
        }

        //internal EventTraceActivity EventTraceActivity
        //{
        //    get
        //    {
        //        if (this.eventTraceActivity == null)
        //        {
        //            this.eventTraceActivity = new EventTraceActivity();
        //        }
        //        return this.eventTraceActivity;
        //    }

        //    set
        //    {
        //        this.eventTraceActivity = value;
        //    }
        //}
    }
}