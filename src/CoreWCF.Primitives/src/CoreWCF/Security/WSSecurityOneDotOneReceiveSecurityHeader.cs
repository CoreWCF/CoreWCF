using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Security
{
    internal class WSSecurityOneDotOneReceiveSecurityHeader : WSSecurityOneDotZeroReceiveSecurityHeader
    {
        public WSSecurityOneDotOneReceiveSecurityHeader(Message message, string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite,
            int headerIndex, MessageDirection direction)
            : base(message, actor, mustUnderstand, relay, standardsManager, algorithmSuite, headerIndex, direction)
        {
        }
    }
}
