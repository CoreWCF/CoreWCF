// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Constants for XML Encryption.
    /// Definitions for namespace, attributes and elements as defined in http://www.w3.org/TR/2002/REC-xmlenc-core-2002120
    /// Only constants that are absent in S.IM
    /// </summary>
    internal static class XmlEncryptionConstants
    {
        public const string Namespace = "http://www.w3.org/2001/04/xmlenc#";
        public const string Prefix    = "xenc";

        public static class Attributes
        {
            public const string Algorithm = "Algorithm";
            public const string Encoding  = "Encoding";
            public const string Id        = "Id";
            public const string MimeType  = "MimeType";
            public const string Recipient = "Recipient";
            public const string Type      = "Type";
            public const string Uri       = "URI";
        }

        public static class Elements
        {
            public const string CarriedKeyName       = "CarriedKeyName";
            public const string CipherData           = "CipherData";
            public const string CipherReference      = "CiperReference";
            public const string CipherValue          = "CipherValue";
            public const string DataReference        = "DataReference";
            public const string EncryptedData        = "EncryptedData";
            public const string EncryptedKey         = "EncryptedKey";
            public const string EncryptionMethod     = "EncryptionMethod";
            public const string EncryptionProperties = "EncryptionProperties";
            public const string KeyReference         = "KeyReference";
            public const string KeySize              = "KeySize";
            public const string OaepParams           = "OAEPparams";
            public const string Recipient            = "Recipient";
            public const string ReferenceList        = "ReferenceList";
        }

        public static class EncryptedDataTypes
        {
            public const string Element         = Namespace + "Element";
            public const string Content         = Namespace + "Content";
        }
    }
}
