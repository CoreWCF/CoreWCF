// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security;
using KeyIdentifierEntry = CoreWCF.IdentityModel.Selectors.SecurityTokenSerializer.KeyIdentifierEntry;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class XmlDsigSep2000 : SecurityTokenSerializer.SerializerEntries
    {
        private KeyInfoSerializer _securityTokenSerializer;

        public XmlDsigSep2000(KeyInfoSerializer securityTokenSerializer)
        {
            _securityTokenSerializer = securityTokenSerializer;
        }

        public override void PopulateKeyIdentifierEntries(IList<KeyIdentifierEntry> keyIdentifierEntries)
        {
            keyIdentifierEntries.Add(new KeyInfoEntry(_securityTokenSerializer));
        }

        public override void PopulateKeyIdentifierClauseEntries(IList<SecurityTokenSerializer.KeyIdentifierClauseEntry> keyIdentifierClauseEntries)
        {
            keyIdentifierClauseEntries.Add(new X509CertificateClauseEntry());
        }

        internal class KeyInfoEntry : KeyIdentifierEntry
        {
            private KeyInfoSerializer _securityTokenSerializer;

            public KeyInfoEntry(KeyInfoSerializer securityTokenSerializer)
            {
                _securityTokenSerializer = securityTokenSerializer;
            }

            protected override XmlDictionaryString LocalName
            {
                get
                {
                    return CoreWCF.XD.XmlSignatureDictionary.KeyInfo;
                }
            }

            protected override XmlDictionaryString NamespaceUri
            {
                get
                {
                    return CoreWCF.XD.XmlSignatureDictionary.Namespace;
                }
            }

            public override SecurityKeyIdentifier ReadKeyIdentifierCore(XmlDictionaryReader reader)
            {
                reader.ReadStartElement(LocalName, NamespaceUri);
                SecurityKeyIdentifier keyIdentifier = new SecurityKeyIdentifier();
                while (reader.IsStartElement())
                {
                    SecurityKeyIdentifierClause clause = _securityTokenSerializer.ReadKeyIdentifierClause(reader);
                    if (clause == null)
                    {
                        reader.Skip();
                    }
                    else
                    {
                        keyIdentifier.Add(clause);
                    }
                }
                if (keyIdentifier.Count == 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.ErrorDeserializingKeyIdentifierClause));
                }
                reader.ReadEndElement();
                return keyIdentifier;
            }

            public override bool SupportsCore(SecurityKeyIdentifier keyIdentifier)
            {
                return true;
            }

            public override void WriteKeyIdentifierCore(XmlDictionaryWriter writer, SecurityKeyIdentifier keyIdentifier)
            {
                writer.WriteStartElement(CoreWCF.XD.XmlSignatureDictionary.Prefix.Value, LocalName, NamespaceUri);
                bool clauseWritten = false;
                foreach (SecurityKeyIdentifierClause clause in keyIdentifier)
                {
                    _securityTokenSerializer.InnerSecurityTokenSerializer.WriteKeyIdentifierClause(writer, clause);
                    clauseWritten = true;
                }
                writer.WriteEndElement(); // KeyInfo
                if (!clauseWritten)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityMessageSerializationException(SR.NoKeyInfoClausesToWrite));
                }
            }
        }

        // so far, we only support two types of X509Data directly under KeyInfo  - X509Certificate and X509SKI
        //   <ds:X509Data>
        //     <ds:X509Certificate>...</ds:X509Certificate>
        //      or
        //     <X509SKI>... </X509SKI>
        //   </ds:X509Data>
        // only support 1 certificate right now
        internal class X509CertificateClauseEntry : SecurityTokenSerializer.KeyIdentifierClauseEntry
        {
            protected override XmlDictionaryString LocalName
            {
                get
                {
                    return CoreWCF.XD.XmlSignatureDictionary.X509Data;
                }
            }

            protected override XmlDictionaryString NamespaceUri
            {
                get
                {
                    return CoreWCF.XD.XmlSignatureDictionary.Namespace;
                }
            }

            public override SecurityKeyIdentifierClause ReadKeyIdentifierClauseCore(XmlDictionaryReader reader)
            {
                SecurityKeyIdentifierClause ski = null;
                reader.ReadStartElement(CoreWCF.XD.XmlSignatureDictionary.X509Data, NamespaceUri);
                while (reader.IsStartElement())
                {
                    if (ski == null && reader.IsStartElement(CoreWCF.XD.XmlSignatureDictionary.X509Certificate, NamespaceUri))
                    {
                        X509Certificate2 certificate = null;
                        if (!SecurityUtils.TryCreateX509CertificateFromRawData(reader.ReadElementContentAsBase64(), out certificate))
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityMessageSerializationException(SR.InvalidX509RawData));
                        }
                        ski = new X509RawDataKeyIdentifierClause(certificate);
                    }
                    else if (ski == null && reader.IsStartElement(XmlSignatureStrings.X509Ski, NamespaceUri.ToString()))
                    {
                        ski = new X509SubjectKeyIdentifierClause(reader.ReadElementContentAsBase64());
                    }
                    else if ((ski == null) && reader.IsStartElement(CoreWCF.XD.XmlSignatureDictionary.X509IssuerSerial, CoreWCF.XD.XmlSignatureDictionary.Namespace))
                    {
                        reader.ReadStartElement(CoreWCF.XD.XmlSignatureDictionary.X509IssuerSerial, CoreWCF.XD.XmlSignatureDictionary.Namespace);
                        reader.ReadStartElement(CoreWCF.XD.XmlSignatureDictionary.X509IssuerName, CoreWCF.XD.XmlSignatureDictionary.Namespace);
                        string issuerName = reader.ReadContentAsString();
                        reader.ReadEndElement();
                        reader.ReadStartElement(CoreWCF.XD.XmlSignatureDictionary.X509SerialNumber, CoreWCF.XD.XmlSignatureDictionary.Namespace);
                        string serialNumber = reader.ReadContentAsString();
                        reader.ReadEndElement();
                        reader.ReadEndElement();

                        ski = new X509IssuerSerialKeyIdentifierClause(issuerName, serialNumber);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                reader.ReadEndElement();
                return ski;
            }

            public override bool SupportsCore(SecurityKeyIdentifierClause keyIdentifierClause)
            {
                return (keyIdentifierClause is X509RawDataKeyIdentifierClause);
                // This method should not write X509IssuerSerialKeyIdentifierClause or X509SubjectKeyIdentifierClause as that should be written by the WSSecurityXXX classes with SecurityTokenReference tag. 
                // The XmlDsig entries are written by the X509SecurityTokenHandler.
            }

            public override void WriteKeyIdentifierClauseCore(XmlDictionaryWriter writer, SecurityKeyIdentifierClause keyIdentifierClause)
            {
                X509RawDataKeyIdentifierClause x509Clause = keyIdentifierClause as X509RawDataKeyIdentifierClause;

                if (x509Clause != null)
                {
                    writer.WriteStartElement(CoreWCF.XD.XmlSignatureDictionary.Prefix.Value, CoreWCF.XD.XmlSignatureDictionary.X509Data, NamespaceUri);

                    writer.WriteStartElement(CoreWCF.XD.XmlSignatureDictionary.Prefix.Value, CoreWCF.XD.XmlSignatureDictionary.X509Certificate, NamespaceUri);
                    byte[] certBytes = x509Clause.GetX509RawData();
                    writer.WriteBase64(certBytes, 0, certBytes.Length);
                    writer.WriteEndElement();

                    writer.WriteEndElement();
                }

                X509IssuerSerialKeyIdentifierClause issuerSerialClause = keyIdentifierClause as X509IssuerSerialKeyIdentifierClause;
                if (issuerSerialClause != null)
                {
                    writer.WriteStartElement(CoreWCF.XD.XmlSignatureDictionary.Prefix.Value, CoreWCF.XD.XmlSignatureDictionary.X509Data, CoreWCF.XD.XmlSignatureDictionary.Namespace);
                    writer.WriteStartElement(CoreWCF.XD.XmlSignatureDictionary.Prefix.Value, CoreWCF.XD.XmlSignatureDictionary.X509IssuerSerial, CoreWCF.XD.XmlSignatureDictionary.Namespace);
                    writer.WriteElementString(CoreWCF.XD.XmlSignatureDictionary.Prefix.Value, CoreWCF.XD.XmlSignatureDictionary.X509IssuerName, CoreWCF.XD.XmlSignatureDictionary.Namespace, issuerSerialClause.IssuerName);
                    writer.WriteElementString(CoreWCF.XD.XmlSignatureDictionary.Prefix.Value, CoreWCF.XD.XmlSignatureDictionary.X509SerialNumber, CoreWCF.XD.XmlSignatureDictionary.Namespace, issuerSerialClause.IssuerSerialNumber);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    return;
                }

                X509SubjectKeyIdentifierClause skiClause = keyIdentifierClause as X509SubjectKeyIdentifierClause;
                if (skiClause != null)
                {
                    writer.WriteStartElement(XmlSignatureConstants.Prefix, XmlSignatureConstants.Elements.X509Data, XmlSignatureConstants.Namespace);
                    writer.WriteStartElement(XmlSignatureConstants.Prefix, XmlSignatureConstants.Elements.X509SKI, XmlSignatureConstants.Namespace);
                    byte[] ski = skiClause.GetX509SubjectKeyIdentifier();
                    writer.WriteBase64(ski, 0, ski.Length);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    return;
                }
            }
        }
    }
}
