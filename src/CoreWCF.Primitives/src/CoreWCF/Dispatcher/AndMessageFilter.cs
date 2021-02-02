// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal class AndMessageFilter : MessageFilter
    {
        public AndMessageFilter(MessageFilter filter1, MessageFilter filter2)
        {
            Filter1 = filter1 ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter1));
            Filter2 = filter2 ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter2));
        }

        public MessageFilter Filter1 { get; }

        public MessageFilter Filter2 { get; }

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

            return Filter1.Match(message) && Filter2.Match(message);
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
                return Filter2.Match(message);
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

            return Filter1.Match(messageBuffer) && Filter2.Match(messageBuffer);
        }
    }
}