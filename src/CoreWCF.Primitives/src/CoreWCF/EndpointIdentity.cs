using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.IdentityModel.Claims;
using System.Security.Cryptography.X509Certificates;

namespace CoreWCF
{
    public abstract class EndpointIdentity
    {
        private Claim _identityClaim;
        private IEqualityComparer<Claim> _claimComparer;

        protected EndpointIdentity()
        {
        }

        internal void Initialize(Claim identityClaim)
        {
            if (identityClaim == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identityClaim));

            Initialize(identityClaim, null);
        }

        internal void Initialize(Claim identityClaim, IEqualityComparer<Claim> claimComparer)
        {
            if (identityClaim == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identityClaim));

            _identityClaim = identityClaim;
            _claimComparer = claimComparer;
        }

        internal Claim IdentityClaim
        {
            get
            {
                if (_identityClaim == null)
                {
                    EnsureIdentityClaim();
                }
                return _identityClaim;
            }
        }

        public static EndpointIdentity CreateIdentity(Claim identity)
        {
            if (identity == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identity));

            if (identity.ClaimType.Equals(ClaimTypes.Dns))
            {
                return new DnsEndpointIdentity(identity);
            }
            else if (identity.ClaimType.Equals(ClaimTypes.Spn))
            {
                throw new PlatformNotSupportedException();
                //return new SpnEndpointIdentity(identity);
            }
            else if (identity.ClaimType.Equals(ClaimTypes.Upn))
            {
                throw new PlatformNotSupportedException();
                //return new UpnEndpointIdentity(identity);
            }
            else if (identity.ClaimType.Equals(ClaimTypes.Rsa))
            {
                throw new PlatformNotSupportedException();
                //return new RsaEndpointIdentity(identity);
            }
            else
            {
                return new GeneralEndpointIdentity(identity);
            }
        }

        internal static EndpointIdentity CreateDnsIdentity(string dnsName)
        {
            return new DnsEndpointIdentity(dnsName);
        }

        internal static EndpointIdentity CreateSpnIdentity(string spnName)
        {
            return new SpnEndpointIdentity(spnName);
        }

        internal static EndpointIdentity CreateUpnIdentity(string upnName)
        {
            return new UpnEndpointIdentity(upnName);
        }

        public static EndpointIdentity CreateX509CertificateIdentity(X509Certificate2 certificate)
        {
            return new X509CertificateEndpointIdentity(certificate);
        }

        internal virtual void EnsureIdentityClaim()
        {
        }

        public override bool Equals(object obj)
        {
            if (obj == (object)this)
                return true;

            // as handles null do we need the double null check?
            if (obj == null)
                return false;

            EndpointIdentity otherIdentity = obj as EndpointIdentity;
            if (otherIdentity == null)
                return false;

            return Matches(otherIdentity.IdentityClaim);
        }

        public override int GetHashCode()
        {
            return GetClaimComparer().GetHashCode(IdentityClaim);
        }

        internal bool Matches(Claim claim)
        {
            return GetClaimComparer().Equals(IdentityClaim, claim);
        }

        private IEqualityComparer<Claim> GetClaimComparer()
        {
            if (_claimComparer == null)
            {
                throw new PlatformNotSupportedException("EndpointIdentity.GetClaimComparer is not supported.");
            }

            return _claimComparer;
        }


        internal static EndpointIdentity ReadIdentity(XmlDictionaryReader reader)
        {
            if (reader == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));

            EndpointIdentity readIdentity = null;

            reader.MoveToContent();
            if (reader.IsEmptyElement)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnexpectedEmptyElementExpectingClaim, XD.AddressingDictionary.Identity.Value, XD.AddressingDictionary.IdentityExtensionNamespace.Value)));

            reader.ReadStartElement(XD.AddressingDictionary.Identity, XD.AddressingDictionary.IdentityExtensionNamespace);

            //if (reader.IsStartElement(XD.AddressingDictionary.Spn, XD.AddressingDictionary.IdentityExtensionNamespace))
            //{
            //    readIdentity = new SpnEndpointIdentity(reader.ReadElementString());
            //}
            //else if (reader.IsStartElement(XD.AddressingDictionary.Upn, XD.AddressingDictionary.IdentityExtensionNamespace))
            //{
            //    readIdentity = new UpnEndpointIdentity(reader.ReadElementString());
            //}
            //else 
            if (reader.IsStartElement(XD.AddressingDictionary.Dns, XD.AddressingDictionary.IdentityExtensionNamespace))
            {
                readIdentity = new DnsEndpointIdentity(reader.ReadElementString());
            }
            else if (reader.IsStartElement(XD.XmlSignatureDictionary.KeyInfo, XD.XmlSignatureDictionary.Namespace))
            {
                reader.ReadStartElement();
                if (reader.IsStartElement(XD.XmlSignatureDictionary.X509Data, XD.XmlSignatureDictionary.Namespace))
                {
                    throw new PlatformNotSupportedException("EndpointIdentity.ReadIdentity X509CertificateEndpointIdentity is not supported.");
                    //readIdentity = new X509CertificateEndpointIdentity(reader);
                }
                else if (reader.IsStartElement(XD.XmlSignatureDictionary.RsaKeyValue, XD.XmlSignatureDictionary.Namespace))
                {
                    throw new PlatformNotSupportedException("EndpointIdentity.ReadIdentity RsaEndpointIdentity is not supported.");
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnrecognizedIdentityType, reader.Name, reader.NamespaceURI)));
                }
                //reader.ReadEndElement();
            }
            else if (reader.NodeType == XmlNodeType.Element)
            {
                //
                // Something unknown
                // 
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnrecognizedIdentityType, reader.Name, reader.NamespaceURI)));
            }
            else
            {
                //
                // EndpointIdentity element is empty or some other invalid xml
                //
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.InvalidIdentityElement));
            }

            reader.ReadEndElement();

            return readIdentity;
        }

        internal void WriteTo(XmlDictionaryWriter writer)
        {
            if (writer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));

            writer.WriteStartElement(XD.AddressingDictionary.Identity, XD.AddressingDictionary.IdentityExtensionNamespace);

            WriteContentsTo(writer);

            writer.WriteEndElement();
        }

        internal virtual void WriteContentsTo(XmlDictionaryWriter writer)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.UnrecognizedIdentityPropertyType, IdentityClaim.GetType().ToString())));
        }
    }

}