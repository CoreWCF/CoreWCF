using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    abstract class HeaderFilter : MessageFilter
    {
        protected HeaderFilter()
            : base()
        {
        }

        public override bool Match(MessageBuffer buffer)
        {
            if (buffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("buffer");
            }

            Message message = buffer.CreateMessage();
            try
            {
                return Match(message);
            }
            finally
            {
                message.Close();
            }
        }
    }
}
