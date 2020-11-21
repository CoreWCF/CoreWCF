using CoreWCF.Channels;
using System.Xml;

namespace CoreWCF.Security
{
    internal interface ISecureConversationSession : ISecuritySession
    {
        void WriteSessionTokenIdentifier(XmlDictionaryWriter writer);
        bool TryReadSessionTokenIdentifier(XmlReader reader);
    }
}
