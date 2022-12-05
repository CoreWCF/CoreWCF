// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.Common.Configuration;

namespace CoreWCF.Queue.Tests.Fakes
{
    internal class FakeBindingElement : QueueBaseTransportBindingElement
    {
        public override BindingElement Clone() => throw new NotImplementedException();

        public override string Scheme { get; }

        public override QueueTransportPump BuildQueueTransportPump(BindingContext context) =>
            throw new System.NotImplementedException();
    }
}
