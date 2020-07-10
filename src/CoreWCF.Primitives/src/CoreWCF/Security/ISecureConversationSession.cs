
namespace CoreWCF.Security
{
    using System.Xml;
    using CoreWCF;
    using CoreWCF.Channels;

    public interface ISecureConversationSession : ISecuritySession
    {
        void WriteSessionTokenIdentifier(XmlDictionaryWriter writer);
        bool TryReadSessionTokenIdentifier(XmlReader reader);
    }
}
