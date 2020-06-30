namespace CoreWCF.Dispatcher
{
    using System;
    using CoreWCF.Channels;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Runtime.Serialization;
    using CoreWCF.Diagnostics;

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
