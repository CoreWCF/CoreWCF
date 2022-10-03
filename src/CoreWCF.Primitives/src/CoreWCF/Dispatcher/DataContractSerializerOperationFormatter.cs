// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Runtime.Serialization;

namespace CoreWCF.Dispatcher
{
    internal static class DataContractSerializerDefaults
    {
        internal const bool IgnoreExtensionDataObject = false;
        internal const int MaxItemsInObjectGraph = int.MaxValue;

        internal static DataContractSerializer CreateSerializer(Type type, int maxItems)
        {
            return CreateSerializer(type, null, maxItems);
        }

        internal static DataContractSerializer CreateSerializer(Type type, IList<Type> knownTypes, int maxItems)
        {
            return new DataContractSerializer(
                type,
                knownTypes);
        }

        internal static DataContractSerializer CreateSerializer(Type type, string rootName, string rootNs, int maxItems)
        {
            return CreateSerializer(type, null, rootName, rootNs, maxItems);
        }

        internal static DataContractSerializer CreateSerializer(Type type, IList<Type> knownTypes, string rootName, string rootNs, int maxItems)
        {
            XmlDictionary dictionary = new XmlDictionary(2);
            return new DataContractSerializer(
                type,
                dictionary.Add(rootName),
                dictionary.Add(rootNs),
                knownTypes);
        }

        internal static DataContractSerializer CreateSerializer(Type type, XmlDictionaryString rootName, XmlDictionaryString rootNs, int maxItems)
        {
            return CreateSerializer(type, null, rootName, rootNs, maxItems);
        }

        internal static DataContractSerializer CreateSerializer(Type type, IList<Type> knownTypes, XmlDictionaryString rootName, XmlDictionaryString rootNs, int maxItems)
        {
            return new DataContractSerializer(
                type,
                rootName,
                rootNs,
                knownTypes);
        }
    }

    public class DataContractSerializerOperationFormatter : OperationFormatter
    {
        private static readonly Type s_typeOfIQueryable = typeof(IQueryable);
        private static readonly Type s_typeOfIQueryableGeneric = typeof(IQueryable<>);
        private static readonly Type s_typeOfIEnumerable = typeof(IEnumerable);
        private static readonly Type s_typeOfIEnumerableGeneric = typeof(IEnumerable<>);

        protected MessageInfo requestMessageInfo;
        protected MessageInfo replyMessageInfo;
        private readonly IList<Type> _knownTypes;

        private IXsdDataContractExporter _dataContractExporter;
        private readonly DataContractSerializerOperationBehavior _serializerFactory;

        public DataContractSerializerOperationFormatter(OperationDescription description, DataContractFormatAttribute dataContractFormatAttribute,
            DataContractSerializerOperationBehavior serializerFactory)
            : base(description, dataContractFormatAttribute.Style == OperationFormatStyle.Rpc, false/*isEncoded*/)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }

            _serializerFactory = serializerFactory ?? new DataContractSerializerOperationBehavior(description);
            foreach (Type type in description.KnownTypes)
            {
                if (_knownTypes == null)
                {
                    _knownTypes = new List<Type>();
                }

                if (type == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxKnownTypeNull, description.Name)));
                }

                ValidateDataContractType(type);
                _knownTypes.Add(type);
            }
            requestMessageInfo = CreateMessageInfo(dataContractFormatAttribute, RequestDescription, _serializerFactory);
            if (ReplyDescription != null)
            {
                replyMessageInfo = CreateMessageInfo(dataContractFormatAttribute, ReplyDescription, _serializerFactory);
            }
        }

        private MessageInfo CreateMessageInfo(DataContractFormatAttribute dataContractFormatAttribute,
            MessageDescription messageDescription, DataContractSerializerOperationBehavior serializerFactory)
        {
            if (messageDescription.IsUntypedMessage)
            {
                return null;
            }

            MessageInfo messageInfo = new MessageInfo();

            MessageBodyDescription body = messageDescription.Body;
            if (body.WrapperName != null)
            {
                messageInfo.WrapperName = AddToDictionary(body.WrapperName);
                messageInfo.WrapperNamespace = AddToDictionary(body.WrapperNamespace);
            }
            MessagePartDescriptionCollection parts = body.Parts;
            messageInfo.BodyParts = new PartInfo[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                messageInfo.BodyParts[i] = CreatePartInfo(parts[i], dataContractFormatAttribute.Style, serializerFactory);
            }

            if (IsValidReturnValue(messageDescription.Body.ReturnValue))
            {
                messageInfo.ReturnPart = CreatePartInfo(messageDescription.Body.ReturnValue, dataContractFormatAttribute.Style, serializerFactory);
            }

            messageInfo.HeaderDescriptionTable = new MessageHeaderDescriptionTable();
            messageInfo.HeaderParts = new PartInfo[messageDescription.Headers.Count];
            for (int i = 0; i < messageDescription.Headers.Count; i++)
            {
                MessageHeaderDescription headerDescription = messageDescription.Headers[i];
                if (headerDescription.IsUnknownHeaderCollection)
                {
                    messageInfo.UnknownHeaderDescription = headerDescription;
                }
                else
                {
                    ValidateDataContractType(headerDescription.Type);
                    messageInfo.HeaderDescriptionTable.Add(headerDescription.Name, headerDescription.Namespace, headerDescription);
                }
                messageInfo.HeaderParts[i] = CreatePartInfo(headerDescription, OperationFormatStyle.Document, serializerFactory);
            }
            messageInfo.AnyHeaders = messageInfo.UnknownHeaderDescription != null || messageInfo.HeaderDescriptionTable.Count > 0;
            return messageInfo;
        }

        private void ValidateDataContractType(Type type)
        {
            if (_dataContractExporter == null)
            {
                _dataContractExporter = XsdDataContractExporterFactory.Create();
                //if (_serializerFactory != null && _serializerFactory.DataContractSurrogate != null)
                //{
                //    ExportOptions options = new ExportOptions();
                //    options.DataContractSurrogate = serializerFactory.DataContractSurrogate;
                //    dataContractExporter.Options = options;
                //}
            }
            _dataContractExporter.GetSchemaTypeName(type); //Throws if the type is not a valid data contract
        }

        private PartInfo CreatePartInfo(MessagePartDescription part, OperationFormatStyle style, DataContractSerializerOperationBehavior serializerFactory)
        {
            string ns = (style == OperationFormatStyle.Rpc || part.Namespace == null) ? string.Empty : part.Namespace;
            PartInfo partInfo = new PartInfo(part, AddToDictionary(part.Name), AddToDictionary(ns), _knownTypes, serializerFactory);
            ValidateDataContractType(partInfo.ContractType);
            return partInfo;
        }

        protected override void AddHeadersToMessage(Message message, MessageDescription messageDescription, object[] parameters, bool isRequest)
        {
            MessageInfo messageInfo = isRequest ? requestMessageInfo : replyMessageInfo;
            PartInfo[] headerParts = messageInfo.HeaderParts;
            if (headerParts == null || headerParts.Length == 0)
            {
                return;
            }

            MessageHeaders headers = message.Headers;
            for (int i = 0; i < headerParts.Length; i++)
            {
                PartInfo headerPart = headerParts[i];
                MessageHeaderDescription headerDescription = (MessageHeaderDescription)headerPart.Description;
                object headerValue = parameters[headerDescription.Index];

                if (headerDescription.Multiple)
                {
                    if (headerValue != null)
                    {
                        bool isXmlElement = headerDescription.Type == typeof(XmlElement);
                        foreach (object headerItemValue in (IEnumerable)headerValue)
                        {
                            AddMessageHeaderForParameter(headers, headerPart, message.Version, headerItemValue, isXmlElement);
                        }
                    }
                }
                else
                {
                    AddMessageHeaderForParameter(headers, headerPart, message.Version, headerValue, false/*isXmlElement*/);
                }
            }
        }

        private void AddMessageHeaderForParameter(MessageHeaders headers, PartInfo headerPart, MessageVersion messageVersion, object parameterValue, bool isXmlElement)
        {
            MessageHeaderDescription headerDescription = (MessageHeaderDescription)headerPart.Description;
            object valueToSerialize = GetContentOfMessageHeaderOfT(headerDescription, parameterValue, out bool mustUnderstand, out bool relay, out string actor);

            if (isXmlElement)
            {
                if (valueToSerialize == null)
                {
                    return;
                }

                XmlElement xmlElement = (XmlElement)valueToSerialize;
                headers.Add(new XmlElementMessageHeader(this, messageVersion, xmlElement.LocalName, xmlElement.NamespaceURI, mustUnderstand, actor, relay, xmlElement));
                return;
            }
            headers.Add(new DataContractSerializerMessageHeader(headerPart, valueToSerialize, mustUnderstand, actor, relay));
        }

        protected override void SerializeBody(XmlDictionaryWriter writer, MessageVersion version, string action, MessageDescription messageDescription, object returnValue, object[] parameters, bool isRequest)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(writer)));
            }

            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(parameters)));
            }

            MessageInfo messageInfo;
            if (isRequest)
            {
                messageInfo = requestMessageInfo;
            }
            else
            {
                messageInfo = replyMessageInfo;
            }

            if (messageInfo.WrapperName != null)
            {
                writer.WriteStartElement(messageInfo.WrapperName, messageInfo.WrapperNamespace);
            }

            if (messageInfo.ReturnPart != null)
            {
                SerializeParameter(writer, messageInfo.ReturnPart, returnValue);
            }

            SerializeParameters(writer, messageInfo.BodyParts, parameters);
            if (messageInfo.WrapperName != null)
            {
                writer.WriteEndElement();
            }
        }

        private void SerializeParameters(XmlDictionaryWriter writer, PartInfo[] parts, object[] parameters)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                PartInfo part = parts[i];
                object graph = parameters[part.Description.Index];
                SerializeParameter(writer, part, graph);
            }
        }

        private void SerializeParameter(XmlDictionaryWriter writer, PartInfo part, object graph)
        {
            if (part.Description.Multiple)
            {
                if (graph != null)
                {
                    foreach (object item in (IEnumerable)graph)
                    {
                        SerializeParameterPart(writer, part, item);
                    }
                }
            }
            else
            {
                SerializeParameterPart(writer, part, graph);
            }
        }

        private void SerializeParameterPart(XmlDictionaryWriter writer, PartInfo part, object graph)
        {
            try
            {
                part.Serializer.WriteObject(writer, graph);
            }
            catch (SerializationException sx)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.Format(SR.SFxInvalidMessageBodyErrorSerializingParameter, part.Description.Namespace, part.Description.Name, sx.Message), sx));
            }
        }

        protected override void GetHeadersFromMessage(Message message, MessageDescription messageDescription, object[] parameters, bool isRequest)
        {
            MessageInfo messageInfo = isRequest ? requestMessageInfo : replyMessageInfo;
            if (!messageInfo.AnyHeaders)
            {
                return;
            }

            MessageHeaders headers = message.Headers;
            KeyValuePair<Type, ArrayList>[] multipleHeaderValues = null;
            ArrayList elementList = null;
            if (messageInfo.UnknownHeaderDescription != null)
            {
                elementList = new ArrayList();
            }

            for (int i = 0; i < headers.Count; i++)
            {
                MessageHeaderInfo header = headers[i];
                MessageHeaderDescription headerDescription = messageInfo.HeaderDescriptionTable.Get(header.Name, header.Namespace);
                if (headerDescription != null)
                {
                    if (header.MustUnderstand)
                    {
                        headers.UnderstoodHeaders.Add(header);
                    }

                    object item = null;
                    XmlDictionaryReader headerReader = headers.GetReaderAtHeader(i);
                    try
                    {
                        object dataValue = DeserializeHeaderContents(headerReader, messageDescription, headerDescription);
                        if (headerDescription.TypedHeader)
                        {
                            item = TypedHeaderManager.Create(headerDescription.Type, dataValue, headers[i].MustUnderstand, headers[i].Relay, headers[i].Actor);
                        }
                        else
                        {
                            item = dataValue;
                        }
                    }
                    finally
                    {
                        headerReader.Dispose();
                    }

                    if (headerDescription.Multiple)
                    {
                        if (multipleHeaderValues == null)
                        {
                            multipleHeaderValues = new KeyValuePair<Type, ArrayList>[parameters.Length];
                        }

                        if (multipleHeaderValues[headerDescription.Index].Key == null)
                        {
                            multipleHeaderValues[headerDescription.Index] = new KeyValuePair<System.Type, System.Collections.ArrayList>(headerDescription.TypedHeader ? TypedHeaderManager.GetMessageHeaderType(headerDescription.Type) : headerDescription.Type, new ArrayList());
                        }
                        multipleHeaderValues[headerDescription.Index].Value.Add(item);
                    }
                    else
                    {
                        parameters[headerDescription.Index] = item;
                    }
                }
                else if (messageInfo.UnknownHeaderDescription != null)
                {
                    MessageHeaderDescription unknownHeaderDescription = messageInfo.UnknownHeaderDescription;
                    XmlDictionaryReader headerReader = headers.GetReaderAtHeader(i);
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        object dataValue = doc.ReadNode(headerReader);
                        if (dataValue != null && unknownHeaderDescription.TypedHeader)
                        {
                            dataValue = TypedHeaderManager.Create(unknownHeaderDescription.Type, dataValue, headers[i].MustUnderstand, headers[i].Relay, headers[i].Actor);
                        }

                        elementList.Add(dataValue);
                    }
                    finally
                    {
                        headerReader.Dispose();
                    }
                }
            }
            if (multipleHeaderValues != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (multipleHeaderValues[i].Key != null)
                    {
                        parameters[i] = multipleHeaderValues[i].Value.ToArray(multipleHeaderValues[i].Key);
                    }
                }
            }
            if (messageInfo.UnknownHeaderDescription != null)
            {
                parameters[messageInfo.UnknownHeaderDescription.Index] = elementList.ToArray(messageInfo.UnknownHeaderDescription.TypedHeader ? typeof(MessageHeader<XmlElement>) : typeof(XmlElement));
            }
        }

        private object DeserializeHeaderContents(XmlDictionaryReader reader, MessageDescription messageDescription, MessageHeaderDescription headerDescription)
        {
            Type dataContractType = GetSubstituteDataContractType(headerDescription.Type, out bool isQueryable);
            XmlObjectSerializer serializerLocal = _serializerFactory.CreateSerializer(dataContractType, headerDescription.Name, headerDescription.Namespace, _knownTypes);
            object val = serializerLocal.ReadObject(reader);
            if (isQueryable && val != null)
            {
                return Queryable.AsQueryable((IEnumerable)val);
            }
            return val;
        }

        protected override object DeserializeBody(XmlDictionaryReader reader, MessageVersion version, string action, MessageDescription messageDescription, object[] parameters, bool isRequest)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(reader)));
            }

            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(parameters)));
            }

            MessageInfo messageInfo;
            if (isRequest)
            {
                messageInfo = requestMessageInfo;
            }
            else
            {
                messageInfo = replyMessageInfo;
            }

            if (messageInfo.WrapperName != null)
            {
                if (!reader.IsStartElement(messageInfo.WrapperName, messageInfo.WrapperNamespace))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.Format(SR.SFxInvalidMessageBody, messageInfo.WrapperName, messageInfo.WrapperNamespace, reader.NodeType, reader.Name, reader.NamespaceURI)));
                }

                bool isEmptyElement = reader.IsEmptyElement;
                reader.Read();
                if (isEmptyElement)
                {
                    return null;
                }
            }
            object returnValue = null;
            if (messageInfo.ReturnPart != null)
            {
                while (true)
                {
                    PartInfo part = messageInfo.ReturnPart;
                    if (part.Serializer.IsStartObject(reader))
                    {
                        returnValue = DeserializeParameter(reader, part, isRequest);
                        break;
                    }
                    if (!reader.IsStartElement())
                    {
                        break;
                    }

                    TraceAndSkipElement(reader);
                }
            }
            DeserializeParameters(reader, messageInfo.BodyParts, parameters, isRequest);
            if (messageInfo.WrapperName != null)
            {
                reader.ReadEndElement();
            }

            return returnValue;
        }

        private void DeserializeParameters(XmlDictionaryReader reader, PartInfo[] parts, object[] parameters, bool isRequest)
        {
            int nextPartIndex = 0;
            while (reader.IsStartElement())
            {
                for (int i = nextPartIndex; i < parts.Length; i++)
                {
                    PartInfo part = parts[i];
                    if (part.Serializer.IsStartObject(reader))
                    {
                        object parameterValue = DeserializeParameter(reader, part, isRequest);
                        parameters[part.Description.Index] = parameterValue;
                        nextPartIndex = i + 1;
                    }
                    else
                    {
                        parameters[part.Description.Index] = null;
                    }
                }

                if (reader.IsStartElement())
                {
                    TraceAndSkipElement(reader);
                }
            }
        }

        private object DeserializeParameter(XmlDictionaryReader reader, PartInfo part, bool isRequest)
        {
            if (part.Description.Multiple)
            {
                ArrayList items = new ArrayList();
                while (part.Serializer.IsStartObject(reader))
                {
                    items.Add(DeserializeParameterPart(reader, part, isRequest));
                }

                return items.ToArray(part.Description.Type);
            }
            return DeserializeParameterPart(reader, part, isRequest);
        }

        private object DeserializeParameterPart(XmlDictionaryReader reader, PartInfo part, bool isRequest)
        {
            object val;
            try
            {
                val = part.ReadObject(reader);
            }
            catch (System.InvalidOperationException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameter, part.Description.Namespace, part.Description.Name), e));
            }
            catch (System.Runtime.Serialization.InvalidDataContractException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataContractException(
                    SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameter, part.Description.Namespace, part.Description.Name), e));
            }
            catch (System.FormatException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    NetDispatcherFaultException.CreateDeserializationFailedFault(
                        SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameterMore, part.Description.Namespace, part.Description.Name, e.Message),
                                     e));
            }
            catch (System.Runtime.Serialization.SerializationException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    NetDispatcherFaultException.CreateDeserializationFailedFault(
                        SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameterMore, part.Description.Namespace, part.Description.Name, e.Message),
                                     e));
            }

            return val;
        }

        internal static Type GetSubstituteDataContractType(Type type, out bool isQueryable)
        {
            if (type == s_typeOfIQueryable)
            {
                isQueryable = true;
                return s_typeOfIEnumerable;
            }

            if (type.GetTypeInfo().IsGenericType &&
                type.GetGenericTypeDefinition() == s_typeOfIQueryableGeneric)
            {
                isQueryable = true;
                return s_typeOfIEnumerableGeneric.MakeGenericType(type.GetGenericArguments());
            }

            isQueryable = false;
            return type;
        }

        private class DataContractSerializerMessageHeader : XmlObjectSerializerHeader
        {
            private readonly PartInfo _headerPart;

            public DataContractSerializerMessageHeader(PartInfo headerPart, object headerValue, bool mustUnderstand, string actor, bool relay)
                : base(headerPart.DictionaryName.Value, headerPart.DictionaryNamespace.Value, headerValue, headerPart.Serializer, mustUnderstand, actor ?? string.Empty, relay)
            {
                _headerPart = headerPart;
            }

            protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                //Prefix needed since there may be xsi:type attribute at toplevel with qname value where ns = ""
                string prefix = (Namespace == null || Namespace.Length == 0) ? string.Empty : "h";
                writer.WriteStartElement(prefix, _headerPart.DictionaryName, _headerPart.DictionaryNamespace);
                WriteHeaderAttributes(writer, messageVersion);
            }
        }


        protected class MessageInfo
        {
            internal PartInfo[] HeaderParts;
            public XmlDictionaryString WrapperName;
            public XmlDictionaryString WrapperNamespace;
            public PartInfo[] BodyParts;
            public PartInfo ReturnPart;
            internal MessageHeaderDescriptionTable HeaderDescriptionTable;
            internal MessageHeaderDescription UnknownHeaderDescription;
            internal bool AnyHeaders;
        }

        protected class PartInfo
        {
            private XmlObjectSerializer _serializer;
            private readonly IList<Type> _knownTypes;
            private readonly DataContractSerializerOperationBehavior _serializerFactory;
            private readonly bool _isQueryable;

            public PartInfo(MessagePartDescription description, XmlDictionaryString dictionaryName, XmlDictionaryString dictionaryNamespace,
                IList<Type> knownTypes, DataContractSerializerOperationBehavior behavior)
            {
                DictionaryName = dictionaryName;
                DictionaryNamespace = dictionaryNamespace;
                Description = description;
                _knownTypes = knownTypes;
                _serializerFactory = behavior;

                ContractType = GetSubstituteDataContractType(description.Type, out _isQueryable);
            }

            public Type ContractType { get; }

            public MessagePartDescription Description { get; }

            public XmlDictionaryString DictionaryName { get; }

            public XmlDictionaryString DictionaryNamespace { get; }

            public XmlObjectSerializer Serializer
            {
                get
                {
                    if (_serializer == null)
                    {
                        _serializer = _serializerFactory.CreateSerializer(ContractType, DictionaryName, DictionaryNamespace, _knownTypes);
                    }
                    return _serializer;
                }
            }

            public object ReadObject(XmlDictionaryReader reader)
            {
                return ReadObject(reader, Serializer);
            }

            public object ReadObject(XmlDictionaryReader reader, XmlObjectSerializer serializer)
            {
                object val = _serializer.ReadObject(reader, false /* verifyObjectName */);
                if (_isQueryable && val != null)
                {
                    return Queryable.AsQueryable((IEnumerable)val);
                }
                return val;
            }
        }
    }
}
