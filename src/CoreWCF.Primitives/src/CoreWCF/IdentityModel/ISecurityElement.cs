// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.IdentityModel
{
    internal interface ISecurityElement
    {
        bool HasId { get; }

        string Id { get; }

        void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager);
    }
}
