// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    interface IAcceptorSecuritySessionProtocol
    {
        bool ReturnCorrelationState { get; set; }
        SecurityToken GetOutgoingSessionToken();
        void SetOutgoingSessionToken(SecurityToken token);
        void SetSessionTokenAuthenticator(UniqueId sessionId, SecurityTokenAuthenticator sessionTokenAuthenticator, SecurityTokenResolver sessionTokenResolver);
    }
}
