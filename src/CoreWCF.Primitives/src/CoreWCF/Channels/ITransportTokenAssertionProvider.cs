// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Channels
{
    public interface ITransportTokenAssertionProvider
    {
        XmlElement GetTransportTokenAssertion();
    }
}
