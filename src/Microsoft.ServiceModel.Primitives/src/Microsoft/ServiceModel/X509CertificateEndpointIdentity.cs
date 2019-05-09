using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace Microsoft.ServiceModel
{
    public class X509CertificateEndpointIdentity : EndpointIdentity
    {
        X509Certificate2Collection certificateCollection = new X509Certificate2Collection();

        public X509CertificateEndpointIdentity(X509Certificate2 certificate)
        {
            if (certificate == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("certificate");

            base.Initialize(new Claim(ClaimTypes.Thumbprint, certificate.GetCertHash(), Rights.PossessProperty));

            certificateCollection.Add(certificate);
        }

        public X509CertificateEndpointIdentity(X509Certificate2 primaryCertificate, X509Certificate2Collection supportingCertificates)
        {
            if (primaryCertificate == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("primaryCertificate");

            if (supportingCertificates == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("supportingCertificates");

            base.Initialize(new Claim(ClaimTypes.Thumbprint, primaryCertificate.GetCertHash(), Rights.PossessProperty));

            certificateCollection.Add(primaryCertificate);

            for (int i = 0; i < supportingCertificates.Count; ++i)
            {
                certificateCollection.Add(supportingCertificates[i]);
            }
        }

        internal X509CertificateEndpointIdentity(XmlDictionaryReader reader)
        {
            if (reader == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("reader");

            reader.MoveToContent();
            if (reader.IsEmptyElement)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnexpectedEmptyElementExpectingClaim, XD.AddressingDictionary.X509v3Certificate.Value, XD.AddressingDictionary.IdentityExtensionNamespace.Value)));

            reader.ReadStartElement(XD.XmlSignatureDictionary.X509Data, XD.XmlSignatureDictionary.Namespace);
            while (reader.IsStartElement(XD.XmlSignatureDictionary.X509Certificate, XD.XmlSignatureDictionary.Namespace))
            {
                X509Certificate2 certificate = new X509Certificate2(Convert.FromBase64String(reader.ReadElementString()));
                if (certificateCollection.Count == 0)
                {
                    // This is the first certificate. We assume this as the primary 
                    // certificate and initialize the base class.
                    base.Initialize(new Claim(ClaimTypes.Thumbprint, certificate.GetCertHash(), Rights.PossessProperty));
                }

                certificateCollection.Add(certificate);
            }

            reader.ReadEndElement();

            if (certificateCollection.Count == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnexpectedEmptyElementExpectingClaim, XD.AddressingDictionary.X509v3Certificate.Value, XD.AddressingDictionary.IdentityExtensionNamespace.Value)));
        }

        public X509Certificate2Collection Certificates
        {
            get { return certificateCollection; }
        }

        internal override void WriteContentsTo(XmlDictionaryWriter writer)
        {
            if (writer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("writer");

            writer.WriteStartElement(XD.XmlSignatureDictionary.Prefix.Value, XD.XmlSignatureDictionary.KeyInfo, XD.XmlSignatureDictionary.Namespace);
            writer.WriteStartElement(XD.XmlSignatureDictionary.Prefix.Value, XD.XmlSignatureDictionary.X509Data, XD.XmlSignatureDictionary.Namespace);
            for (int i = 0; i < certificateCollection.Count; ++i)
            {
                writer.WriteElementString(XD.XmlSignatureDictionary.X509Certificate, XD.XmlSignatureDictionary.Namespace, Convert.ToBase64String(certificateCollection[i].RawData));
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }

}
