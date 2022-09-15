// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Runtime;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal class MultiplexingDispatchMessageFormatter : IDispatchMessageFormatter
    {
        private readonly Dictionary<WebMessageFormat, IDispatchMessageFormatter> _formatters;

        public WebMessageFormat DefaultFormat { get; }

        public Dictionary<WebMessageFormat, string> DefaultContentTypes { get; }

        public MultiplexingDispatchMessageFormatter(Dictionary<WebMessageFormat, IDispatchMessageFormatter> formatters, WebMessageFormat defaultFormat)
        {
            _formatters = formatters ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(formatters));
            DefaultFormat = defaultFormat;
            DefaultContentTypes = new Dictionary<WebMessageFormat, string>();

            Fx.Assert(_formatters.ContainsKey(DefaultFormat), "The default format should always be included in the dictionary of formatters.");
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SerializingRequestNotSupportedByFormatter, this)));
        }

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
        {
            WebOperationContext currentContext = WebOperationContext.Current;
            OutgoingWebResponseContext outgoingResponse = null;

            if (currentContext != null)
            {
                outgoingResponse = currentContext.OutgoingResponse;
            }

            WebMessageFormat format = DefaultFormat;
            if (outgoingResponse != null)
            {
                WebMessageFormat? nullableFormat = outgoingResponse.Format;
                if (nullableFormat.HasValue)
                {
                    format = nullableFormat.Value;
                }
            }

            if (!_formatters.ContainsKey(format))
            {
                string operationName = "<null>";

                if (OperationContext.Current != null)
                {
                    MessageProperties messageProperties = OperationContext.Current.IncomingMessageProperties;
                    if (messageProperties.ContainsKey(WebHttpDispatchOperationSelector.HttpOperationNamePropertyName))
                    {
                        operationName = messageProperties[WebHttpDispatchOperationSelector.HttpOperationNamePropertyName] as string;
                    }
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.OperationDoesNotSupportFormat, operationName, format.ToString())));
            }

            if (outgoingResponse != null && string.IsNullOrEmpty(outgoingResponse.ContentType))
            {
                string automatedSelectionContentType = outgoingResponse.AutomatedFormatSelectionContentType;
                if (!string.IsNullOrEmpty(automatedSelectionContentType))
                {
                    // Don't set the content-type if it is default xml for backwards compatiabilty
                    if (!string.Equals(automatedSelectionContentType, DefaultContentTypes[WebMessageFormat.Xml], StringComparison.OrdinalIgnoreCase))
                    {
                        outgoingResponse.ContentType = automatedSelectionContentType;
                    }
                }
                else
                {
                    // Don't set the content-type if it is default xml for backwards compatiabilty
                    if (format != WebMessageFormat.Xml)
                    {
                        outgoingResponse.ContentType = DefaultContentTypes[format];
                    }
                }
            }

            Message message = _formatters[format].SerializeReply(messageVersion, parameters, result);

            return message;
        }

        public bool SupportsMessageFormat(WebMessageFormat format) => _formatters.ContainsKey(format);
    }
}
