// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Channels
{
    internal class BinaryVersion
    {
        public static readonly BinaryVersion Version1 = new BinaryVersion(FramingEncodingString.Binary, FramingEncodingString.BinarySession, ServiceModelDictionary.Version1);
        public static readonly BinaryVersion GZipVersion1 = new BinaryVersion(FramingEncodingString.ExtendedBinaryGZip, FramingEncodingString.ExtendedBinarySessionGZip, ServiceModelDictionary.Version1);
        public static readonly BinaryVersion DeflateVersion1 = new BinaryVersion(FramingEncodingString.ExtendedBinaryDeflate, FramingEncodingString.ExtendedBinarySessionDeflate, ServiceModelDictionary.Version1);

        private BinaryVersion(string contentType, string sessionContentType, IXmlDictionary dictionary)
        {
            ContentType = contentType;
            SessionContentType = sessionContentType;
            Dictionary = dictionary;
        }

        public static BinaryVersion CurrentVersion { get { return Version1; } }
        public string ContentType { get; }
        public string SessionContentType { get; }
        public IXmlDictionary Dictionary { get; }
    }
}
