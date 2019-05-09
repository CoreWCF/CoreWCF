using System;
using System.Xml;
using Microsoft.IdentityModel.Claims;

namespace Microsoft.ServiceModel
{
    internal class DnsEndpointIdentity : EndpointIdentity
    {
        public DnsEndpointIdentity(string dnsName)
        {
            if (dnsName == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("dnsName");

            base.Initialize(Claim.CreateDnsClaim(dnsName));
        }

        public DnsEndpointIdentity(Claim identity)
        {
            if (identity == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("identity");

            if (!identity.ClaimType.Equals(ClaimTypes.Dns))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.UnrecognizedClaimTypeForIdentity, identity.ClaimType, ClaimTypes.Dns));

            base.Initialize(identity);
        }

        internal override void WriteContentsTo(XmlDictionaryWriter writer)
        {
            if (writer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("writer");

            writer.WriteElementString(XD.AddressingDictionary.Dns, XD.AddressingDictionary.IdentityExtensionNamespace, (string)IdentityClaim.Resource);
        }
    }
}