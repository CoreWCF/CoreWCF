using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class AndMessageFilter : MessageFilter
    {
        MessageFilter filter1;
        MessageFilter filter2;

        public AndMessageFilter(MessageFilter filter1, MessageFilter filter2)
        {
            if (filter1 == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter1");
            if (filter2 == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter2");

            this.filter1 = filter1;
            this.filter2 = filter2;
        }

        public MessageFilter Filter1
        {
            get
            {
                return filter1;
            }
        }

        public MessageFilter Filter2
        {
            get
            {
                return filter2;
            }
        }

        protected internal override IMessageFilterTable<FilterData> CreateFilterTable<FilterData>()
        {
            return new AndMessageFilterTable<FilterData>();
        }

        public override bool Match(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }

            return filter1.Match(message) && filter2.Match(message);
        }

        internal bool Match(Message message, out bool addressMatched)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }

            if (filter1.Match(message))
            {
                addressMatched = true;
                return filter2.Match(message);
            }
            else
            {
                addressMatched = false;
                return false;
            }
        }

        public override bool Match(MessageBuffer messageBuffer)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("messageBuffer");
            }

            return filter1.Match(messageBuffer) && filter2.Match(messageBuffer);
        }
    }

}