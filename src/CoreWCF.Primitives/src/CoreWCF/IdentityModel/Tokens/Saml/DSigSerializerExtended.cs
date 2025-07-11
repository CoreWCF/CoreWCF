// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Xml;

namespace CoreWCF.IdentityModel.Tokens.Saml
{
    internal class DSigSerializerExtended : DSigSerializer
    {
        protected override bool TryReadKeyInfoType(XmlReader reader, ref Microsoft.IdentityModel.Xml.KeyInfo keyInfo)
        {
            if (keyInfo is KeyInfo keyInfoType)
            {

                if (TryReadSecurityTokenReference(reader, out SecurityTokenReference securityTokenReference))
                {
                    keyInfoType.SecurityTokenReference = securityTokenReference;
                    return true;
                }
                else if (TryReadBinarySecret(reader, out BinarySecret secret))
                {
                    keyInfoType.BinarySecret = secret;
                    return true;
                }
                else if (reader.IsStartElement(XmlEncryptionStrings.EncryptedKey))
                {
                    if (TryReadEncryptedKey(reader, out var encryptedKey))
                    {
                        keyInfoType.EncryptedKey = encryptedKey;
                        return true;
                    }
                    else
                        return false;
                }
            }

            return base.TryReadKeyInfoType(reader, ref keyInfo);
        }

        private static bool TryReadBinarySecret(XmlReader reader, out BinarySecret secret)
        {
            secret = null;
            if (!reader.IsStartElement("BinarySecret", "http://docs.oasis-open.org/ws-sx/ws-trust/200512"))
                return false;

            secret = new BinarySecret(reader.ReadElementContentAsString());

            return true;
        }

        protected override Microsoft.IdentityModel.Xml.KeyInfo CreateKeyInfo(XmlReader reader)
        {
            Microsoft.IdentityModel.Xml.XmlUtil.CheckReaderOnEntry(reader, XmlSignatureConstants.Elements.KeyInfo, XmlSignatureConstants.Namespace);

            return new KeyInfo
            {
                Prefix = reader.Prefix
            };
        }

        /// <summary>
        /// Reads the "SecurityTokenReference" element
        /// </summary>
        /// <param name="reader">A <see cref="XmlReader"/> positioned on a <see cref="CoreWcfXmlSignatureConstants.Elements.SecurityTokenReference"/> element.</param>
        /// <param name="securityTokenReference"></param>
        private static bool TryReadSecurityTokenReference(XmlReader reader, out SecurityTokenReference securityTokenReference)
        {
            securityTokenReference = null;
            if (!reader.IsStartElement(CoreWCF.XD.SecurityJan2004Dictionary.SecurityTokenReference.Value, CoreWCF.XD.SecurityJan2004Dictionary.Namespace.Value))
                return false;

            reader.ReadStartElement(CoreWCF.XD.SecurityJan2004Dictionary.SecurityTokenReference.Value, CoreWCF.XD.SecurityJan2004Dictionary.Namespace.Value);

            if (!reader.IsStartElement(CoreWCF.XD.SecurityJan2004Dictionary.KeyIdentifier.Value, CoreWCF.XD.SecurityJan2004Dictionary.Namespace.Value))
                return false;

            string valueType = reader.GetAttribute(CoreWCF.XD.SecurityJan2004Dictionary.ValueType.Value, null);
            string encodingType = reader.GetAttribute(CoreWCF.XD.SecurityJan2004Dictionary.EncodingType.Value, null);
            string value = reader.ReadElementContentAsString(CoreWCF.XD.SecurityJan2004Dictionary.KeyIdentifier.Value, CoreWCF.XD.SecurityJan2004Dictionary.Namespace.Value);

            reader.ReadEndElement();

            securityTokenReference = new SecurityTokenReference(new SecurityKeyIdentifier(valueType, encodingType, value));

            return true;
        }

        /// <summary>
        /// Attempts to read the <see cref="CoreWcfXmlSignatureConstants.Elements.EncryptedKey"/>
        /// </summary>
        /// <param name="reader">A <see cref="XmlReader"/> positioned on a <see cref="CoreWcfXmlSignatureConstants.Elements.EncryptedKey"/> element.</param>
        /// <param name="encryptedKey">The parsed <see cref="CoreWcfXmlSignatureConstants.Elements.EncryptedKey"/> element.</param>
        protected virtual bool TryReadEncryptedKey(XmlReader reader, out EncryptedKey encryptedKey)
        {
            if (reader == null)
                throw new ArgumentNullException(LogHelper.FormatInvariant("IDX10000: The parameter '{0}' cannot be a 'null' or an empty object. ", nameof(reader)));

            encryptedKey = null;

            if (!reader.IsStartElement(XmlEncryptionStrings.EncryptedKey, XmlSignatureStrings.Namespace))
                return false;

            reader.ReadStartElement(XmlEncryptionStrings.EncryptedKey, XmlSignatureStrings.Namespace);

            if (!reader.IsStartElement(XmlEncryptionStrings.EncryptionMethod, XmlSignatureStrings.Namespace))
                throw Microsoft.IdentityModel.Xml.XmlUtil.LogReadException(
                    "IDX30011: Unable to read XML. Expecting XmlReader to be at ns.element: '{0}.{1}', found: '{2}.{3}'.",
                    XmlSignatureStrings.Namespace,
                    XmlEncryptionStrings.EncryptionMethod,
                    reader.NamespaceURI,
                    reader.LocalName);

            string algorithm = reader.GetAttribute(XmlEncryptionStrings.AlgorithmAttribute);
            Microsoft.IdentityModel.Xml.KeyInfo keyInfo = ReadKeyInfo(reader);

            if (!reader.IsStartElement(XmlEncryptionStrings.CipherData, XmlSignatureStrings.Namespace))
                throw Microsoft.IdentityModel.Xml.XmlUtil.LogReadException(
                    "IDX30011: Unable to read XML. Expecting XmlReader to be at ns.element: '{0}.{1}', found: '{2}.{3}'.",
                    XmlSignatureStrings.Namespace,
                    XmlEncryptionStrings.CipherData,
                    reader.NamespaceURI,
                    reader.LocalName);

            reader.ReadStartElement(XmlEncryptionStrings.CipherData, XmlSignatureStrings.Namespace);

            if (!reader.IsStartElement(XmlEncryptionStrings.CipherValue, XmlSignatureStrings.Namespace))
                throw Microsoft.IdentityModel.Xml.XmlUtil.LogReadException(
                    "IDX30011: Unable to read XML. Expecting XmlReader to be at ns.element: '{0}.{1}', found: '{2}.{3}'.",
                    XmlSignatureStrings.Namespace,
                    XmlEncryptionStrings.CipherValue,
                    reader.NamespaceURI,
                    reader.LocalName);

            string cipherValue = reader.ReadElementContentAsString(XmlEncryptionStrings.CipherValue, XmlSignatureStrings.Namespace);
            reader.ReadEndElement();
            encryptedKey = new EncryptedKey(algorithm, keyInfo, cipherValue);

            return true;
        }
    }
}
