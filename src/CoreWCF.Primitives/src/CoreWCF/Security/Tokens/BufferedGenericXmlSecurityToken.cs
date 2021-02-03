// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    internal class BufferedGenericXmlSecurityToken : GenericXmlSecurityToken
    {
        public BufferedGenericXmlSecurityToken(
            XmlElement tokenXml,
            SecurityToken proofToken,
            DateTime effectiveTime,
            DateTime expirationTime,
            SecurityKeyIdentifierClause internalTokenReference,
            SecurityKeyIdentifierClause externalTokenReference,
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies,
            XmlBuffer tokenXmlBuffer
            )
            : base(tokenXml, proofToken, effectiveTime, expirationTime, internalTokenReference, externalTokenReference, authorizationPolicies)
        {
            TokenXmlBuffer = tokenXmlBuffer;
        }

        public XmlBuffer TokenXmlBuffer { get; }
    }
}
