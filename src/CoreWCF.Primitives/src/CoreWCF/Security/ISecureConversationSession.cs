// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    internal interface ISecureConversationSession : ISecuritySession
    {
        void WriteSessionTokenIdentifier(XmlDictionaryWriter writer);
        bool TryReadSessionTokenIdentifier(XmlReader reader);
    }
}
