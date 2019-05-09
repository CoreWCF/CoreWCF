using System.Xml;

namespace Microsoft.ServiceModel.Channels
{
    class BinaryVersion
    {
        static public readonly BinaryVersion Version1 = new BinaryVersion(FramingEncodingString.Binary, FramingEncodingString.BinarySession, ServiceModelDictionary.Version1);
        static public readonly BinaryVersion GZipVersion1 = new BinaryVersion(FramingEncodingString.ExtendedBinaryGZip, FramingEncodingString.ExtendedBinarySessionGZip, ServiceModelDictionary.Version1);
        static public readonly BinaryVersion DeflateVersion1 = new BinaryVersion(FramingEncodingString.ExtendedBinaryDeflate, FramingEncodingString.ExtendedBinarySessionDeflate, ServiceModelDictionary.Version1);
        private readonly IXmlDictionary dictionary;

        BinaryVersion(string contentType, string sessionContentType, IXmlDictionary dictionary)
        {
            this.ContentType = contentType;
            this.SessionContentType = sessionContentType;
            this.dictionary = dictionary;
        }

        static public BinaryVersion CurrentVersion { get { return Version1; } }
        public string ContentType { get; }
        public string SessionContentType { get; }
        public IXmlDictionary Dictionary { get { return dictionary; } }
    }
}
