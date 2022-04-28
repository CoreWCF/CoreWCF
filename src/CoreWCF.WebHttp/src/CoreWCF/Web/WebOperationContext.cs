// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace CoreWCF.Web
{
    public class WebOperationContext : IExtension<OperationContext>
    {
        internal static readonly string s_defaultTextMediaType = "text/plain";
        internal static readonly string s_defaultJsonMediaType = JsonGlobals.ApplicationJsonMediaType;
        internal static readonly string s_defaultXmlMediaType = "application/xml";
        internal static readonly string s_defaultAtomMediaType = "application/atom+xml";
        internal static readonly string s_defaultStreamMediaType = WebHttpBehavior.s_defaultStreamContentType;

        private readonly OperationContext _operationContext;

        public WebOperationContext(OperationContext operationContext)
        {
            _operationContext = operationContext ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operationContext));

            if (operationContext.Extensions.Find<WebOperationContext>() == null)
            {
                operationContext.Extensions.Add(this);
            }
        }

        public static WebOperationContext Current
        {
            get
            {
                if (OperationContext.Current == null)
                {
                    return null;
                }

                WebOperationContext existing = OperationContext.Current.Extensions.Find<WebOperationContext>();
                if (existing != null)
                {
                    return existing;
                }

                return new WebOperationContext(OperationContext.Current);
            }
        }

        public IncomingWebRequestContext IncomingRequest => new IncomingWebRequestContext(_operationContext);

        public OutgoingWebResponseContext OutgoingResponse => new OutgoingWebResponseContext(_operationContext);

        public void Attach(OperationContext owner)
        {
        }

        public void Detach(OperationContext owner)
        {
        }

        public Message CreateJsonResponse<T>(T instance)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));

            return CreateJsonResponse(instance, serializer);
        }

        public Message CreateJsonResponse<T>(T instance, DataContractJsonSerializer serializer)
        {
            if (serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            Message message = Message.CreateMessage(MessageVersion.None, null, instance, serializer);
            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.JsonProperty);
            AddContentType(s_defaultJsonMediaType, OutgoingResponse.BindingWriteEncoding);

            return message;
        }

        public Message CreateXmlResponse<T>(T instance)
        {
            System.Runtime.Serialization.DataContractSerializer serializer = new System.Runtime.Serialization.DataContractSerializer(typeof(T));

            return CreateXmlResponse(instance, serializer);
        }

        public Message CreateXmlResponse<T>(T instance, System.Runtime.Serialization.XmlObjectSerializer serializer)
        {
            if (serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            Message message = Message.CreateMessage(MessageVersion.None, null, instance, serializer);
            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.XmlProperty);
            AddContentType(s_defaultXmlMediaType, OutgoingResponse.BindingWriteEncoding);

            return message;
        }

        public Message CreateXmlResponse<T>(T instance, XmlSerializer serializer)
        {
            if (serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            Message message = Message.CreateMessage(MessageVersion.None, null, new XmlSerializerBodyWriter(instance, serializer));
            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.XmlProperty);
            AddContentType(s_defaultXmlMediaType, OutgoingResponse.BindingWriteEncoding);

            return message;
        }

        public Message CreateXmlResponse(XDocument document)
        {
            if (document == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(document));
            }

            Message message;
            if (document.FirstNode == null)
            {
                message = Message.CreateMessage(MessageVersion.None, null);
            }
            else
            {
                message = Message.CreateMessage(MessageVersion.None, (string)null, document.CreateReader());
            }

            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.XmlProperty);
            AddContentType(s_defaultXmlMediaType, OutgoingResponse.BindingWriteEncoding);

            return message;
        }

        public Message CreateXmlResponse(XElement element)
        {
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            Message message = Message.CreateMessage(MessageVersion.None, null, element.CreateReader());
            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.XmlProperty);
            AddContentType(s_defaultXmlMediaType, OutgoingResponse.BindingWriteEncoding);

            return message;
        }

        public Message CreateTextResponse(string text) => CreateTextResponse(text, s_defaultTextMediaType, Encoding.UTF8);

        public Message CreateTextResponse(string text, string contentType) => CreateTextResponse(text, contentType, Encoding.UTF8);

        public Message CreateTextResponse(string text, string contentType, Encoding encoding)
        {
            if (text == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(text));
            }

            if (contentType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contentType));
            }

            if (encoding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encoding));
            }

            Message message = new HttpStreamMessage(ActionOfStreamBodyWriter.CreateStreamBodyWriter((stream) =>
            {
                byte[] preamble = encoding.GetPreamble();
                if (preamble.Length > 0)
                {
                    stream.Write(preamble, 0, preamble.Length);
                }
                byte[] bytes = encoding.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }));
            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.RawProperty);
            AddContentType(contentType, null);

            return message;
        }

        public Message CreateTextResponse(Action<TextWriter> textWriter, string contentType)
        {
            Encoding encoding = OutgoingResponse.BindingWriteEncoding;
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            return CreateTextResponse(textWriter, contentType, encoding);
        }

        public Message CreateTextResponse(Action<TextWriter> textWriter, string contentType, Encoding encoding)
        {
            if (textWriter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(textWriter));
            }

            if (contentType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contentType));
            }

            if (encoding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encoding));
            }

            Message message = new HttpStreamMessage(ActionOfStreamBodyWriter.CreateStreamBodyWriter((stream) =>
            {
                using (TextWriter writer = new StreamWriter(stream, encoding))
                {
                    textWriter(writer);
                }
            }));
            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.RawProperty);
            AddContentType(contentType, null);

            return message;
        }

        public Message CreateStreamResponse(Stream stream, string contentType)
        {
            if (stream == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            }

            if (contentType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contentType));
            }

            Message message = ByteStreamMessage.CreateMessage(stream);
            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.RawProperty);
            AddContentType(contentType, null);

            return message;
        }

        public Message CreateStreamResponse(StreamBodyWriter bodyWriter, string contentType)
        {
            if (bodyWriter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bodyWriter));
            }

            if (contentType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contentType));
            }

            Message message = new HttpStreamMessage(bodyWriter);
            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.RawProperty);
            AddContentType(contentType, null);

            return message;
        }

        public Message CreateStreamResponse(Action<Stream> streamWriter, string contentType)
        {
            if (streamWriter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(streamWriter));
            }

            if (contentType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contentType));
            }

            Message message = new HttpStreamMessage(ActionOfStreamBodyWriter.CreateStreamBodyWriter(streamWriter));
            message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.RawProperty);
            AddContentType(contentType, null);

            return message;
        }

        public UriTemplate GetUriTemplate(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operationName));
            }

            if (!(OperationContext.Current.EndpointDispatcher.DispatchRuntime.OperationSelector is WebHttpDispatchOperationSelector selector))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.OperationSelectorNotWebSelector, typeof(WebHttpDispatchOperationSelector))));
            }

            return selector.GetUriTemplate(operationName);
        }

        private void AddContentType(string contentType, Encoding encoding)
        {
            if (string.IsNullOrEmpty(OutgoingResponse.ContentType))
            {
                if (encoding != null)
                {
                    contentType = WebMessageEncoderFactory.GetContentType(contentType, encoding);
                }

                OutgoingResponse.ContentType = contentType;
            }
        }

        internal class XmlSerializerBodyWriter : BodyWriter
        {
            private readonly object _instance;
            private readonly XmlSerializer _serializer;

            public XmlSerializerBodyWriter(object instance, XmlSerializer serializer)
                : base(false)
            {
                _instance = instance ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(instance));
                _serializer = serializer ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
            {
                _serializer.Serialize(writer, _instance);
            }
        }
    }
}
