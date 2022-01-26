// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal class DemultiplexingDispatchMessageFormatter : IDispatchMessageFormatter
    {
        private readonly IDispatchMessageFormatter _defaultFormatter;
        private readonly Dictionary<WebContentFormat, IDispatchMessageFormatter> _formatters;
        private string _supportedFormats;

        public DemultiplexingDispatchMessageFormatter(IDictionary<WebContentFormat, IDispatchMessageFormatter> formatters, IDispatchMessageFormatter defaultFormatter)
        {
            if (formatters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(formatters));
            }

            _formatters = new Dictionary<WebContentFormat, IDispatchMessageFormatter>();
            foreach (WebContentFormat key in formatters.Keys)
            {
                _formatters.Add(key, formatters[key]);
            }

            _defaultFormatter = defaultFormatter;
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            if (message == null)
            {
                return;
            }

            IDispatchMessageFormatter selectedFormatter;
            if (TryGetEncodingFormat(message, out WebContentFormat format))
            {
                _formatters.TryGetValue(format, out selectedFormatter);
                if (selectedFormatter == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.UnrecognizedHttpMessageFormat, format, GetSupportedFormats())));
                }
            }
            else
            {
                selectedFormatter = _defaultFormatter;
                if (selectedFormatter == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.MessageFormatPropertyNotFound3)));
                }
            }

            selectedFormatter.DeserializeRequest(message, parameters);
        }

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SerializingReplyNotSupportedByFormatter, this)));

        internal static string GetSupportedFormats(IEnumerable<WebContentFormat> formats)
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;
            foreach (WebContentFormat format in formats)
            {
                if (i > 0)
                {
                    sb.Append(CultureInfo.CurrentCulture.TextInfo.ListSeparator);
                    sb.Append(" ");
                }
                sb.Append("'" + format.ToString() + "'");
                ++i;
            }

            return sb.ToString();
        }

        internal static bool TryGetEncodingFormat(Message message, out WebContentFormat format)
        {
            message.Properties.TryGetValue(WebBodyFormatMessageProperty.Name, out object prop);
            if (!(prop is WebBodyFormatMessageProperty formatProperty))
            {
                format = WebContentFormat.Default;
                return false;
            }

            format = formatProperty.Format;

            return true;
        }

        private string GetSupportedFormats()
        {
            if (_supportedFormats == null)
            {
                _supportedFormats = GetSupportedFormats(_formatters.Keys);
            }

            return _supportedFormats;
        }
    }
}
