using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using System.Xml;
using System;
using CoreWCF.IdentityModel;

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
            IdentityModel.XmlBuffer tokenXmlBuffer
            )
            : base(tokenXml, proofToken, effectiveTime, expirationTime, internalTokenReference, externalTokenReference, authorizationPolicies)
        {
            TokenXmlBuffer = tokenXmlBuffer;
        }

        public IdentityModel.XmlBuffer TokenXmlBuffer { get; }
    }
}
