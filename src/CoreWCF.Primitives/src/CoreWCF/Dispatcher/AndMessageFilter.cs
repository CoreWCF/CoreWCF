// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal class AndMessageFilter : MessageFilter
    {
        private readonly MessageFilter filter2;

        public AndMessageFilter(MessageFilter filter1, MessageFilter filter2)
        {
            if (filter1 == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter1));
            }

            if (filter2 == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter2));
            }

            Filter1 = filter1;
            this.filter2 = filter2;
        }

        public MessageFilter Filter1 { get; }

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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            return Filter1.Match(message) && filter2.Match(message);
        }

        internal bool Match(Message message, out bool addressMatched)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            if (Filter1.Match(message))
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            return Filter1.Match(messageBuffer) && filter2.Match(messageBuffer);
        }
    }

}