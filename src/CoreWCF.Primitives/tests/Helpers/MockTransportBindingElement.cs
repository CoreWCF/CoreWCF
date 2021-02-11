// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace Helpers
{
    internal class MockTransportBindingElement : TransportBindingElement
    {
        public override string Scheme => "foo";

        public override BindingElement Clone()
        {
            return this;
        }
    }
}
