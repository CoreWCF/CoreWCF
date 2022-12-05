// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Queue.Tests.Fakes
{
    internal class FakeBinding : Binding
    {
        public override string Scheme { get; }
        public override BindingElementCollection CreateBindingElements() => throw new NotImplementedException();
    }
}
