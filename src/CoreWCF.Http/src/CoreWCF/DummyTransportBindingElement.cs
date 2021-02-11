// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF
{
    internal class DummyTransportBindingElement : TransportBindingElement
    {
        public DummyTransportBindingElement()
        {
        }

        public override BindingElement Clone()
        {
            return this;
        }

        public override string Scheme { get { return "dummy"; } }
    }
}