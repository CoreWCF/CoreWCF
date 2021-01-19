
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Security
{
    internal sealed class WSSecurityOneDotOneSendSecurityHeader : WSSecurityOneDotZeroSendSecurityHeader
    {
        public WSSecurityOneDotOneSendSecurityHeader(Message message, string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite,
            MessageDirection direction)
            : base(message, actor, mustUnderstand, relay, standardsManager, algorithmSuite, direction)
        {
        }
    }
}

