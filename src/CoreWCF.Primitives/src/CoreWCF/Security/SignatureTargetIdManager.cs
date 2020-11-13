using System.Xml;

namespace CoreWCF.Security
{
    internal abstract class SignatureTargetIdManager
    {
        public abstract string DefaultIdNamespacePrefix { get; }

        public abstract string DefaultIdNamespaceUri { get; }

        public abstract string ExtractId(XmlDictionaryReader reader);

        public abstract void WriteIdAttribute(XmlDictionaryWriter writer, string id);
    }
}
