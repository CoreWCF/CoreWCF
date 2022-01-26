// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Description;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal abstract class SingleBodyParameterMessageFormatter : IDispatchMessageFormatter
    {
        private readonly bool _isRequestFormatter;
        private readonly string _serializerType;

        protected SingleBodyParameterMessageFormatter(OperationDescription operation, bool isRequestFormatter, string serializerType)
        {
            if (operation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operation));
            }

            ContractName = operation.DeclaringContract.Name;
            ContractNs = operation.DeclaringContract.Namespace;
            OperationName = operation.Name;
            _isRequestFormatter = isRequestFormatter;
            _serializerType = serializerType;
        }

        protected string ContractName { get; }

        protected string ContractNs { get; }

        protected string OperationName { get; }

        public static IDispatchMessageFormatter CreateXmlAndJsonDispatchFormatter(OperationDescription operation, Type type, bool isRequestFormatter, UnwrappedTypesXmlSerializerManager xmlSerializerManager, string callbackParameterName)
        {
            IDispatchMessageFormatter xmlFormatter = CreateDispatchFormatter(operation, type, isRequestFormatter, false, xmlSerializerManager, null);
            if (!WebHttpBehavior.SupportsJsonFormat(operation))
            {
                return xmlFormatter;
            }

            IDispatchMessageFormatter jsonFormatter = CreateDispatchFormatter(operation, type, isRequestFormatter, true, xmlSerializerManager, callbackParameterName);
            Dictionary<WebContentFormat, IDispatchMessageFormatter> map = new Dictionary<WebContentFormat, IDispatchMessageFormatter>
            {
                { WebContentFormat.Xml, xmlFormatter },
                { WebContentFormat.Json, jsonFormatter }
            };

            return new DemultiplexingDispatchMessageFormatter(map, xmlFormatter);
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            if (!_isRequestFormatter)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.FormatterCannotBeUsedForRequestMessages)));
            }

            parameters[0] = ReadObject(message);
        }

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
        {
            if (_isRequestFormatter)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.FormatterCannotBeUsedForReplyMessages)));
            }

            Message message = Message.CreateMessage(messageVersion, (string)null, CreateBodyWriter(result));
            if (result == null)
            {
                SuppressReplyEntityBody(message);
            }

            AttachMessageProperties(message, false);

            return message;
        }

        internal static IDispatchMessageFormatter CreateDispatchFormatter(OperationDescription operation, Type type, bool isRequestFormatter, bool useJson, UnwrappedTypesXmlSerializerManager xmlSerializerManager, string callbackParameterName)
        {
            if (type == null)
            {
                return new NullMessageFormatter();
            }
            else if (useJson)
            {
                return CreateJsonFormatter(operation, type, isRequestFormatter);
            }
            else
            {
                return CreateXmlFormatter(operation, type, isRequestFormatter, xmlSerializerManager);
            }
        }

        internal static void SuppressReplyEntityBody(Message message)
        {
            WebOperationContext currentContext = WebOperationContext.Current;
            if (currentContext != null)
            {
                OutgoingWebResponseContext responseContext = currentContext.OutgoingResponse;
                if (responseContext != null)
                {
                    responseContext.SuppressEntityBody = true;
                }
            }
            else
            {
                message.Properties.TryGetValue(HttpResponseMessageProperty.Name, out object untypedProp);
                if (!(untypedProp is HttpResponseMessageProperty prop))
                {
                    prop = new HttpResponseMessageProperty();
                    message.Properties[HttpResponseMessageProperty.Name] = prop;
                }
                prop.SuppressEntityBody = true;
            }
        }

        protected virtual void AttachMessageProperties(Message message, bool isRequest)
        {
        }

        protected abstract XmlObjectSerializer[] GetInputSerializers();

        protected abstract XmlObjectSerializer GetOutputSerializer(Type type);

        protected virtual void ValidateMessageFormatProperty(Message message)
        {
        }

        protected Type GetTypeForSerializer(Type type, Type parameterType, IList<Type> knownTypes)
        {
            if (type == parameterType)
            {
                return type;
            }
            else if (knownTypes != null)
            {
                for (int i = 0; i < knownTypes.Count; ++i)
                {
                    if (type == knownTypes[i])
                    {
                        return type;
                    }
                }
            }

            return parameterType;
        }

        public static SingleBodyParameterMessageFormatter CreateXmlFormatter(OperationDescription operation, Type type, bool isRequestFormatter, UnwrappedTypesXmlSerializerManager xmlSerializerManager)
        {
            DataContractSerializerOperationBehavior dcsob = ((KeyedByTypeCollection<IOperationBehavior>)operation.OperationBehaviors).Find<DataContractSerializerOperationBehavior>();
            if (dcsob != null)
            {
                return new SingleBodyParameterDataContractMessageFormatter(operation, type, isRequestFormatter, false, dcsob);
            }

            XmlSerializerOperationBehavior xsob = ((KeyedByTypeCollection<IOperationBehavior>)operation.OperationBehaviors).Find<XmlSerializerOperationBehavior>();
            if (xsob != null)
            {
                return new SingleBodyParameterXmlSerializerMessageFormatter(operation, type, isRequestFormatter, xsob, xmlSerializerManager);
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.OnlyDataContractAndXmlSerializerTypesInUnWrappedMode, operation.Name)));
        }

        public static SingleBodyParameterMessageFormatter CreateJsonFormatter(OperationDescription operation, Type type, bool isRequestFormatter)
        {
            DataContractSerializerOperationBehavior dcsob = ((KeyedByTypeCollection<IOperationBehavior>)operation.OperationBehaviors).Find<DataContractSerializerOperationBehavior>();
            if (dcsob == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.JsonFormatRequiresDataContract, operation.Name, operation.DeclaringContract.Name, operation.DeclaringContract.Namespace)));
            }

            return new SingleBodyParameterDataContractMessageFormatter(operation, type, isRequestFormatter, true, dcsob);
        }

        private BodyWriter CreateBodyWriter(object body)
        {
            XmlObjectSerializer serializer;
            if (body != null)
            {
                serializer = GetOutputSerializer(body.GetType());
                if (serializer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.CannotSerializeType, body.GetType(), OperationName, ContractName, ContractNs, _serializerType)));
                }
            }
            else
            {
                serializer = null;
            }

            return new SingleParameterBodyWriter(body, serializer);
        }

        protected virtual object ReadObject(Message message)
        {
            if (HttpStreamFormatter.IsEmptyMessage(message))
            {
                return null;
            }

            XmlObjectSerializer[] inputSerializers = GetInputSerializers();
            XmlDictionaryReader reader = message.GetReaderAtBodyContents();
            if (inputSerializers != null)
            {
                for (int i = 0; i < inputSerializers.Length; ++i)
                {
                    if (inputSerializers[i].IsStartObject(reader))
                    {
                        return inputSerializers[i].ReadObject(reader, false);
                    }
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.Format(SR.CannotDeserializeBody, reader.LocalName, reader.NamespaceURI, OperationName, ContractName, ContractNs, _serializerType)));
        }

        internal class NullMessageFormatter : IDispatchMessageFormatter
        {
            public void DeserializeRequest(Message message, object[] parameters)
            {
            }

            public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
            {
                Message reply = Message.CreateMessage(messageVersion, null);
                SuppressReplyEntityBody(reply);

                return reply;
            }
        }

        internal class SingleParameterBodyWriter : BodyWriter
        {
            private readonly object _body;
            private readonly XmlObjectSerializer _serializer;

            public SingleParameterBodyWriter(object body, XmlObjectSerializer serializer)
                : base(false)
            {
                if (body != null && serializer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
                }

                _body = body;
                _serializer = serializer;
            }

            protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
            {
                if (_body != null)
                {
                    _serializer.WriteObject(writer, _body);
                }
            }
        }
    }
}
