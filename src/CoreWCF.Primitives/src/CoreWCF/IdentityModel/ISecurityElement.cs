

using System.Xml;

namespace CoreWCF.IdentityModel
{
    internal interface ISecurityElement
    {
        bool HasId { get; }

        string Id { get; }

        void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager);
    }
}
