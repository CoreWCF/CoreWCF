// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using CoreWCF.Channels;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal class ContentTypeSettingDispatchMessageFormatter : IDispatchMessageFormatter
    {
        private readonly IDispatchMessageFormatter _innerFormatter;
        private readonly string _outgoingContentType;

        public ContentTypeSettingDispatchMessageFormatter(string outgoingContentType, IDispatchMessageFormatter innerFormatter)
        {
            _outgoingContentType = outgoingContentType ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(outgoingContentType));
            _innerFormatter = innerFormatter ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerFormatter));
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            _innerFormatter.DeserializeRequest(message, parameters);
        }

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
        {
            Message message = _innerFormatter.SerializeReply(messageVersion, parameters, result);
            if (message != null)
            {
                AddResponseContentTypeProperty(message, _outgoingContentType);
            }

            return message;
        }

        private static void AddResponseContentTypeProperty(Message message, string contentType)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            if (contentType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contentType));
            }

            if (OperationContext.Current != null && OperationContext.Current.OutgoingMessageProperties.Count > 0)
            {
                if (string.IsNullOrEmpty(WebOperationContext.Current.OutgoingResponse.ContentType))
                {
                    WebOperationContext.Current.OutgoingResponse.ContentType = contentType;
                }
            }
            else
            {
                message.Properties.TryGetValue(HttpResponseMessageProperty.Name, out object prop);
                HttpResponseMessageProperty httpProperty;

                if (prop != null)
                {
                    httpProperty = (HttpResponseMessageProperty)prop;
                }
                else
                {
                    httpProperty = new HttpResponseMessageProperty();
                    message.Properties.Add(HttpResponseMessageProperty.Name, httpProperty);
                }

                if (string.IsNullOrEmpty(httpProperty.Headers[HttpResponseHeader.ContentType]))
                {
                    httpProperty.Headers[HttpResponseHeader.ContentType] = contentType;
                }
            }
        }
    }
}
