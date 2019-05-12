using System.Runtime.Serialization;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    [DataContract]
    internal class MatchAllMessageFilter : MessageFilter
    {
        public MatchAllMessageFilter()
            : base()
        {
        }

        public override bool Match(MessageBuffer messageBuffer)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("messageBuffer");
            }
            return true;
        }

        public override bool Match(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }
            return true;
        }
    }

}