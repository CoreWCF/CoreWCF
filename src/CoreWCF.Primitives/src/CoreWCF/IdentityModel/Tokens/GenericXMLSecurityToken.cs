// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Xml;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    public class GenericXmlSecurityToken : SecurityToken
    {
        private const int SupportedPersistanceVersion = 1;
        private readonly string id;
        private readonly SecurityToken proofToken;
        private readonly SecurityKeyIdentifierClause internalTokenReference;
        private readonly SecurityKeyIdentifierClause externalTokenReference;
        private readonly XmlElement tokenXml;
        private readonly ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies;
        private readonly DateTime effectiveTime;
        private readonly DateTime expirationTime;

        public GenericXmlSecurityToken(
            XmlElement tokenXml,
            SecurityToken proofToken,
            DateTime effectiveTime,
            DateTime expirationTime,
            SecurityKeyIdentifierClause internalTokenReference,
            SecurityKeyIdentifierClause externalTokenReference,
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies
            )
        {
            if (tokenXml == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenXml));
            }

            id = GetId(tokenXml);
            this.tokenXml = tokenXml;
            this.proofToken = proofToken;
            this.effectiveTime = effectiveTime.ToUniversalTime();
            this.expirationTime = expirationTime.ToUniversalTime();

            this.internalTokenReference = internalTokenReference;
            this.externalTokenReference = externalTokenReference;
            this.authorizationPolicies = authorizationPolicies ?? EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
        }

        public override string Id => id;

        public override DateTime ValidFrom => effectiveTime;

        public override DateTime ValidTo => expirationTime;

        public SecurityKeyIdentifierClause InternalTokenReference => internalTokenReference;

        public SecurityKeyIdentifierClause ExternalTokenReference => externalTokenReference;

        public XmlElement TokenXml => tokenXml;

        public SecurityToken ProofToken => proofToken;

        public ReadOnlyCollection<IAuthorizationPolicy> AuthorizationPolicies => authorizationPolicies;

        public override ReadOnlyCollection<SecurityKey> SecurityKeys
        {
            get
            {
                if (proofToken != null)
                {
                    return proofToken.SecurityKeys;
                }
                else
                {
                    return EmptyReadOnlyCollection<SecurityKey>.Instance;
                }
            }
        }

        public override string ToString()
        {
            StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);
            writer.WriteLine("Generic XML token:");
            writer.WriteLine("   validFrom: {0}", ValidFrom);
            writer.WriteLine("   validTo: {0}", ValidTo);
            if (internalTokenReference != null)
            {
                writer.WriteLine("   InternalTokenReference: {0}", internalTokenReference);
            }

            if (externalTokenReference != null)
            {
                writer.WriteLine("   ExternalTokenReference: {0}", externalTokenReference);
            }

            writer.WriteLine("   Token Element: ({0}, {1})", tokenXml.LocalName, tokenXml.NamespaceURI);
            return writer.ToString();
        }

        private static string GetId(XmlElement tokenXml)
        {
            if (tokenXml != null)
            {
                string id = tokenXml.GetAttribute(UtilityStrings.IdAttribute, UtilityStrings.Namespace);
                if (string.IsNullOrEmpty(id))
                {
                    // special case SAML 1.1 as this is the only possible ID as
                    // spec is closed.  SAML 2.0 is xs:ID
                    id = tokenXml.GetAttribute("AssertionID");

                    // if we are still null, "Id"
                    if (string.IsNullOrEmpty(id))
                    {
                        id = tokenXml.GetAttribute("Id");
                    }

                    //This fixes the unecnrypted SAML 2.0 case. Eg: <Assertion ID="_05955298-214f-41e7-b4c3-84dbff7f01b9" 
                    if (string.IsNullOrEmpty(id))
                    {
                        id = tokenXml.GetAttribute("ID");
                    }
                }

                if (!string.IsNullOrEmpty(id))
                {
                    return id;
                }
            }

            return null;
        }

        public override bool CanCreateKeyIdentifierClause<T>()
        {
            if (internalTokenReference != null && typeof(T) == internalTokenReference.GetType())
            {
                return true;
            }

            if (externalTokenReference != null && typeof(T) == externalTokenReference.GetType())
            {
                return true;
            }

            return false;
        }

        public override T CreateKeyIdentifierClause<T>()
        {
            if (internalTokenReference != null && typeof(T) == internalTokenReference.GetType())
            {
                return (T)internalTokenReference;
            }

            if (externalTokenReference != null && typeof(T) == externalTokenReference.GetType())
            {
                return (T)externalTokenReference;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.UnableToCreateTokenReference)));
        }

        public override bool MatchesKeyIdentifierClause(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (internalTokenReference != null && internalTokenReference.Matches(keyIdentifierClause))
            {
                return true;
            }
            else if (externalTokenReference != null && externalTokenReference.Matches(keyIdentifierClause))
            {
                return true;
            }

            return false;
        }
    }
}
