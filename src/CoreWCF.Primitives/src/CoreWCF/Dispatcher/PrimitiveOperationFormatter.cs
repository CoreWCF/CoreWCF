// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class PrimitiveOperationFormatter : IClientMessageFormatter, IDispatchMessageFormatter
    {
        internal static readonly string NsXsi = "http://www.w3.org/2001/XMLSchema-instance";
        private readonly OperationDescription _operation;
        private readonly MessageDescription _responseMessage;
        private readonly MessageDescription _requestMessage;
        private readonly XmlDictionaryString _action;
        private readonly XmlDictionaryString _replyAction;
        private ActionHeader _actionHeaderNone;
        private ActionHeader _actionHeader10;
        private ActionHeader _replyActionHeaderNone;
        private ActionHeader _replyActionHeader10;
        private readonly XmlDictionaryString _requestWrapperName;
        private readonly XmlDictionaryString _requestWrapperNamespace;
        private readonly XmlDictionaryString _responseWrapperName;
        private readonly XmlDictionaryString _responseWrapperNamespace;
        private readonly PartInfo[] _requestParts;
        private readonly PartInfo[] _responseParts;
        private readonly PartInfo _returnPart;
        private readonly XmlDictionaryString _xsiNilLocalName;
        private readonly XmlDictionaryString _xsiNilNamespace;

        public PrimitiveOperationFormatter(OperationDescription description, bool isRpc)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }

            OperationFormatter.Validate(description, isRpc, false/*isEncoded*/);

            _operation = description;
            _requestMessage = description.Messages[0];
            if (description.Messages.Count == 2)
            {
                _responseMessage = description.Messages[1];
            }

            int stringCount = 3 + _requestMessage.Body.Parts.Count;
            if (_responseMessage != null)
            {
                stringCount += 2 + _responseMessage.Body.Parts.Count;
            }

            XmlDictionary dictionary = new XmlDictionary(stringCount * 2);

            _xsiNilLocalName = dictionary.Add("nil");
            _xsiNilNamespace = dictionary.Add(NsXsi);

            OperationFormatter.GetActions(description, dictionary, out _action, out _replyAction);

            if (_requestMessage.Body.WrapperName != null)
            {
                _requestWrapperName = AddToDictionary(dictionary, _requestMessage.Body.WrapperName);
                _requestWrapperNamespace = AddToDictionary(dictionary, _requestMessage.Body.WrapperNamespace);
            }

            _requestParts = AddToDictionary(dictionary, _requestMessage.Body.Parts, isRpc);

            if (_responseMessage != null)
            {
                if (_responseMessage.Body.WrapperName != null)
                {
                    _responseWrapperName = AddToDictionary(dictionary, _responseMessage.Body.WrapperName);
                    _responseWrapperNamespace = AddToDictionary(dictionary, _responseMessage.Body.WrapperNamespace);
                }

                _responseParts = AddToDictionary(dictionary, _responseMessage.Body.Parts, isRpc);

                if (_responseMessage.Body.ReturnValue != null && _responseMessage.Body.ReturnValue.Type != typeof(void))
                {
                    _returnPart = AddToDictionary(dictionary, _responseMessage.Body.ReturnValue, isRpc);
                }
            }
        }

        private ActionHeader ActionHeaderNone
        {
            get
            {
                if (_actionHeaderNone == null)
                {
                    _actionHeaderNone =
                        ActionHeader.Create(_action, AddressingVersion.None);
                }

                return _actionHeaderNone;
            }
        }

        private ActionHeader ActionHeader10
        {
            get
            {
                if (_actionHeader10 == null)
                {
                    _actionHeader10 =
                        ActionHeader.Create(_action, AddressingVersion.WSAddressing10);
                }

                return _actionHeader10;
            }
        }

        //ActionHeader ActionHeaderAugust2004
        //{
        //    get
        //    {
        //        if (actionHeaderAugust2004 == null)
        //        {
        //            actionHeaderAugust2004 =
        //                ActionHeader.Create(this.action, AddressingVersion.WSAddressingAugust2004);
        //        }

        //        return actionHeaderAugust2004;
        //    }
        //}

        private ActionHeader ReplyActionHeaderNone
        {
            get
            {
                if (_replyActionHeaderNone == null)
                {
                    _replyActionHeaderNone =
                        ActionHeader.Create(_replyAction, AddressingVersion.None);
                }

                return _replyActionHeaderNone;
            }
        }

        private ActionHeader ReplyActionHeader10
        {
            get
            {
                if (_replyActionHeader10 == null)
                {
                    _replyActionHeader10 =
                        ActionHeader.Create(_replyAction, AddressingVersion.WSAddressing10);
                }

                return _replyActionHeader10;
            }
        }

        //ActionHeader ReplyActionHeaderAugust2004
        //{
        //    get
        //    {
        //        if (replyActionHeaderAugust2004 == null)
        //        {
        //            replyActionHeaderAugust2004 =
        //                ActionHeader.Create(this.replyAction, AddressingVersion.WSAddressingAugust2004);
        //        }

        //        return replyActionHeaderAugust2004;
        //    }
        //}

        private static XmlDictionaryString AddToDictionary(XmlDictionary dictionary, string s)
        {
            if (!dictionary.TryLookup(s, out XmlDictionaryString dictionaryString))
            {
                dictionaryString = dictionary.Add(s);
            }
            return dictionaryString;
        }

        private static PartInfo[] AddToDictionary(XmlDictionary dictionary, MessagePartDescriptionCollection parts, bool isRpc)
        {
            PartInfo[] partInfos = new PartInfo[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                partInfos[i] = AddToDictionary(dictionary, parts[i], isRpc);
            }
            return partInfos;
        }

        private ActionHeader GetActionHeader(AddressingVersion addressing)
        {
            if (_action == null)
            {
                return null;
            }

            //if (addressing == AddressingVersion.WSAddressingAugust2004)
            //{
            //    return ActionHeaderAugust2004;
            //}
            //else
            if (addressing == AddressingVersion.WSAddressing10)
            {
                return ActionHeader10;
            }
            else if (addressing == AddressingVersion.None)
            {
                return ActionHeaderNone;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.Format(SR.AddressingVersionNotSupported, addressing)));
            }
        }

        private ActionHeader GetReplyActionHeader(AddressingVersion addressing)
        {
            if (_replyAction == null)
            {
                return null;
            }

            //if (addressing == AddressingVersion.WSAddressingAugust2004)
            //{
            //    return ReplyActionHeaderAugust2004;
            //}
            //else 
            if (addressing == AddressingVersion.WSAddressing10)
            {
                return ReplyActionHeader10;
            }
            else if (addressing == AddressingVersion.None)
            {
                return ReplyActionHeaderNone;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.Format(SR.AddressingVersionNotSupported, addressing)));
            }
        }

        private static string GetArrayItemName(Type type)
        {
            switch (type.GetTypeCode())
            {
                case TypeCode.Boolean:
                    return "boolean";
                case TypeCode.DateTime:
                    return "dateTime";
                case TypeCode.Decimal:
                    return "decimal";
                case TypeCode.Int32:
                    return "int";
                case TypeCode.Int64:
                    return "long";
                case TypeCode.Single:
                    return "float";
                case TypeCode.Double:
                    return "double";
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxInvalidUseOfPrimitiveOperationFormatter));
            }
        }

        private static PartInfo AddToDictionary(XmlDictionary dictionary, MessagePartDescription part, bool isRpc)
        {
            Type type = part.Type;
            XmlDictionaryString itemName = null;
            XmlDictionaryString itemNamespace = null;
            if (type.IsArray && type != typeof(byte[]))
            {
                const string ns = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
                string name = GetArrayItemName(type.GetElementType());
                itemName = AddToDictionary(dictionary, name);
                itemNamespace = AddToDictionary(dictionary, ns);
            }
            return new PartInfo(part,
                AddToDictionary(dictionary, part.Name),
                AddToDictionary(dictionary, isRpc ? string.Empty : part.Namespace),
                itemName, itemNamespace);
        }

        public static bool IsContractSupported(OperationDescription description)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }

            OperationDescription operation = description;
            MessageDescription requestMessage = description.Messages[0];
            MessageDescription responseMessage = null;
            if (description.Messages.Count == 2)
            {
                responseMessage = description.Messages[1];
            }

            if (requestMessage.Headers.Count > 0)
            {
                return false;
            }

            if (requestMessage.Properties.Count > 0)
            {
                return false;
            }

            if (requestMessage.IsTypedMessage)
            {
                return false;
            }

            if (responseMessage != null)
            {
                if (responseMessage.Headers.Count > 0)
                {
                    return false;
                }

                if (responseMessage.Properties.Count > 0)
                {
                    return false;
                }

                if (responseMessage.IsTypedMessage)
                {
                    return false;
                }
            }
            if (!AreTypesSupported(requestMessage.Body.Parts))
            {
                return false;
            }

            if (responseMessage != null)
            {
                if (!AreTypesSupported(responseMessage.Body.Parts))
                {
                    return false;
                }

                if (responseMessage.Body.ReturnValue != null && !IsTypeSupported(responseMessage.Body.ReturnValue))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreTypesSupported(MessagePartDescriptionCollection bodyDescriptions)
        {
            for (int i = 0; i < bodyDescriptions.Count; i++)
            {
                if (!IsTypeSupported(bodyDescriptions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsTypeSupported(MessagePartDescription bodyDescription)
        {
            Fx.Assert(bodyDescription != null, "");
            Type type = bodyDescription.Type;
            if (type == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxMessagePartDescriptionMissingType, bodyDescription.Name, bodyDescription.Namespace)));
            }

            if (bodyDescription.Multiple)
            {
                return false;
            }

            if (type == typeof(void))
            {
                return true;
            }

            if (type.GetTypeInfo().IsEnum)
            {
                return false;
            }

            switch (type.GetTypeCode())
            {
                case TypeCode.Boolean:
                case TypeCode.DateTime:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Single:
                case TypeCode.String:
                    return true;
                case TypeCode.Object:
                    if (type.IsArray && type.GetArrayRank() == 1 && IsArrayTypeSupported(type.GetElementType()))
                    {
                        return true;
                    }

                    break;
                default:
                    break;
            }
            return false;
        }

        private static bool IsArrayTypeSupported(Type type)
        {
            if (type.GetTypeInfo().IsEnum)
            {
                return false;
            }

            switch (type.GetTypeCode())
            {
                case TypeCode.Byte:
                case TypeCode.Boolean:
                case TypeCode.DateTime:
                case TypeCode.Decimal:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Single:
                case TypeCode.Double:
                    return true;
                default:
                    return false;
            }
        }

        public Message SerializeRequest(MessageVersion messageVersion, object[] parameters)
        {
            if (messageVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }

            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            return Message.CreateMessage(messageVersion, GetActionHeader(messageVersion.Addressing), new PrimitiveRequestBodyWriter(parameters, this));
        }

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result)
        {
            if (messageVersion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageVersion));
            }

            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            return Message.CreateMessage(messageVersion, GetReplyActionHeader(messageVersion.Addressing), new PrimitiveResponseBodyWriter(parameters, result, this));
        }

        public object DeserializeReply(Message message, object[] parameters)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(message)));
            }

            if (parameters == null)
            {
                throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(parameters)), message);
            }

            try
            {
                if (message.IsEmpty)
                {
                    if (_responseWrapperName == null)
                    {
                        return null;
                    }

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.SFxInvalidMessageBodyEmptyMessage));
                }

                XmlDictionaryReader bodyReader = message.GetReaderAtBodyContents();
                using (bodyReader)
                {
                    object returnValue = DeserializeResponse(bodyReader, parameters);
                    message.ReadFromBodyContentsToEnd(bodyReader);
                    return returnValue;
                }
            }
            catch (XmlException xe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.Format(SR.SFxErrorDeserializingReplyBodyMore, _operation.Name, xe.Message), xe));
            }
            catch (FormatException fe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.Format(SR.SFxErrorDeserializingReplyBodyMore, _operation.Name, fe.Message), fe));
            }
            catch (SerializationException se)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.Format(SR.SFxErrorDeserializingReplyBodyMore, _operation.Name, se.Message), se));
            }
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(message)));
            }

            if (parameters == null)
            {
                throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(parameters)), message);
            }

            try
            {
                if (message.IsEmpty)
                {
                    if (_requestWrapperName == null)
                    {
                        return;
                    }

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.SFxInvalidMessageBodyEmptyMessage));
                }

                XmlDictionaryReader bodyReader = message.GetReaderAtBodyContents();
                using (bodyReader)
                {
                    DeserializeRequest(bodyReader, parameters);
                    message.ReadFromBodyContentsToEnd(bodyReader);
                }
            }
            catch (XmlException xe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    OperationFormatter.CreateDeserializationFailedFault(
                        SR.Format(SR.SFxErrorDeserializingRequestBodyMore, _operation.Name, xe.Message),
                        xe));
            }
            catch (FormatException fe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    OperationFormatter.CreateDeserializationFailedFault(
                        SR.Format(SR.SFxErrorDeserializingRequestBodyMore, _operation.Name, fe.Message),
                        fe));
            }
            catch (SerializationException se)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.Format(SR.SFxErrorDeserializingRequestBodyMore, _operation.Name, se.Message),
                    se));
            }
        }

        private void DeserializeRequest(XmlDictionaryReader reader, object[] parameters)
        {
            if (_requestWrapperName != null)
            {
                if (!reader.IsStartElement(_requestWrapperName, _requestWrapperNamespace))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.Format(SR.SFxInvalidMessageBody, _requestWrapperName, _requestWrapperNamespace, reader.NodeType, reader.Name, reader.NamespaceURI)));
                }

                bool isEmptyElement = reader.IsEmptyElement;
                reader.Read();
                if (isEmptyElement)
                {
                    return;
                }
            }

            DeserializeParameters(reader, _requestParts, parameters);

            if (_requestWrapperName != null)
            {
                reader.ReadEndElement();
            }
        }

        private object DeserializeResponse(XmlDictionaryReader reader, object[] parameters)
        {
            if (_responseWrapperName != null)
            {
                if (!reader.IsStartElement(_responseWrapperName, _responseWrapperNamespace))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.Format(SR.SFxInvalidMessageBody, _responseWrapperName, _responseWrapperNamespace, reader.NodeType, reader.Name, reader.NamespaceURI)));
                }

                bool isEmptyElement = reader.IsEmptyElement;
                reader.Read();
                if (isEmptyElement)
                {
                    return null;
                }
            }

            object returnValue = null;
            if (_returnPart != null)
            {
                while (true)
                {
                    if (IsPartElement(reader, _returnPart))
                    {
                        returnValue = DeserializeParameter(reader, _returnPart);
                        break;
                    }
                    if (!reader.IsStartElement())
                    {
                        break;
                    }

                    if (IsPartElements(reader, _responseParts))
                    {
                        break;
                    }

                    OperationFormatter.TraceAndSkipElement(reader);
                }
            }
            DeserializeParameters(reader, _responseParts, parameters);

            if (_responseWrapperName != null)
            {
                reader.ReadEndElement();
            }

            return returnValue;
        }

        private void DeserializeParameters(XmlDictionaryReader reader, PartInfo[] parts, object[] parameters)
        {
            if (parts.Length != parameters.Length)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentException(SR.Format(SR.SFxParameterCountMismatch, "parts", parts.Length, "parameters", parameters.Length), nameof(parameters)));
            }

            int nextPartIndex = 0;
            while (reader.IsStartElement())
            {
                for (int i = nextPartIndex; i < parts.Length; i++)
                {
                    PartInfo part = parts[i];
                    if (IsPartElement(reader, part))
                    {
                        parameters[part.Description.Index] = DeserializeParameter(reader, parts[i]);
                        nextPartIndex = i + 1;
                    }
                    else
                    {
                        parameters[part.Description.Index] = null;
                    }
                }

                if (reader.IsStartElement())
                {
                    OperationFormatter.TraceAndSkipElement(reader);
                }
            }
        }

        private bool IsPartElements(XmlDictionaryReader reader, PartInfo[] parts)
        {
            foreach (PartInfo part in parts)
            {
                if (IsPartElement(reader, part))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPartElement(XmlDictionaryReader reader, PartInfo part)
        {
            return reader.IsStartElement(part.DictionaryName, part.DictionaryNamespace);
        }

        private object DeserializeParameter(XmlDictionaryReader reader, PartInfo part)
        {
            if (reader.AttributeCount > 0 &&
                reader.MoveToAttribute(_xsiNilLocalName.Value, _xsiNilNamespace.Value) &&
                reader.ReadContentAsBoolean())
            {
                reader.Skip();
                return null;
            }
            return part.ReadValue(reader);
        }

        private void SerializeParameter(XmlDictionaryWriter writer, PartInfo part, object graph)
        {
            writer.WriteStartElement(part.DictionaryName, part.DictionaryNamespace);
            if (graph == null)
            {
                writer.WriteStartAttribute(_xsiNilLocalName, _xsiNilNamespace);
                writer.WriteValue(true);
                writer.WriteEndAttribute();
            }
            else
            {
                part.WriteValue(writer, graph);
            }

            writer.WriteEndElement();
        }

        private void SerializeParameters(XmlDictionaryWriter writer, PartInfo[] parts, object[] parameters)
        {
            if (parts.Length != parameters.Length)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentException(SR.Format(SR.SFxParameterCountMismatch, "parts", parts.Length, "parameters", parameters.Length), nameof(parameters)));
            }

            for (int i = 0; i < parts.Length; i++)
            {
                PartInfo part = parts[i];
                SerializeParameter(writer, part, parameters[part.Description.Index]);
            }
        }

        private void SerializeRequest(XmlDictionaryWriter writer, object[] parameters)
        {
            if (_requestWrapperName != null)
            {
                writer.WriteStartElement(_requestWrapperName, _requestWrapperNamespace);
            }

            SerializeParameters(writer, _requestParts, parameters);

            if (_requestWrapperName != null)
            {
                writer.WriteEndElement();
            }
        }

        private void SerializeResponse(XmlDictionaryWriter writer, object returnValue, object[] parameters)
        {
            if (_responseWrapperName != null)
            {
                writer.WriteStartElement(_responseWrapperName, _responseWrapperNamespace);
            }

            if (_returnPart != null)
            {
                SerializeParameter(writer, _returnPart, returnValue);
            }

            SerializeParameters(writer, _responseParts, parameters);

            if (_responseWrapperName != null)
            {
                writer.WriteEndElement();
            }
        }

        private class PartInfo
        {
            private readonly XmlDictionaryString _itemName;
            private readonly XmlDictionaryString _itemNamespace;
            private readonly TypeCode _typeCode;
            private readonly bool _isArray;

            public PartInfo(MessagePartDescription description, XmlDictionaryString dictionaryName, XmlDictionaryString dictionaryNamespace, XmlDictionaryString itemName, XmlDictionaryString itemNamespace)
            {
                DictionaryName = dictionaryName;
                DictionaryNamespace = dictionaryNamespace;
                _itemName = itemName;
                _itemNamespace = itemNamespace;
                Description = description;
                if (description.Type.IsArray)
                {
                    _isArray = true;
                    _typeCode = description.Type.GetElementType().GetTypeCode();
                }
                else
                {
                    _isArray = false;
                    _typeCode = description.Type.GetTypeCode();
                }
            }

            public MessagePartDescription Description { get; }

            public XmlDictionaryString DictionaryName { get; }

            public XmlDictionaryString DictionaryNamespace { get; }

            public object ReadValue(XmlDictionaryReader reader)
            {
                object value;
                if (_isArray)
                {
                    switch (_typeCode)
                    {
                        case TypeCode.Byte:
                            value = reader.ReadElementContentAsBase64();
                            break;
                        case TypeCode.Boolean:
                            if (!reader.IsEmptyElement)
                            {
                                reader.ReadStartElement();
                                value = reader.ReadBooleanArray(_itemName, _itemNamespace);
                                reader.ReadEndElement();
                            }
                            else
                            {
                                reader.Read();
                                value = new bool[0];
                            }
                            break;
                        case TypeCode.DateTime:
                            if (!reader.IsEmptyElement)
                            {
                                reader.ReadStartElement();
                                value = reader.ReadDateTimeArray(_itemName, _itemNamespace);
                                reader.ReadEndElement();
                            }
                            else
                            {
                                reader.Read();
                                value = new DateTime[0];
                            }
                            break;
                        case TypeCode.Decimal:
                            if (!reader.IsEmptyElement)
                            {
                                reader.ReadStartElement();
                                value = reader.ReadDecimalArray(_itemName, _itemNamespace);
                                reader.ReadEndElement();
                            }
                            else
                            {
                                reader.Read();
                                value = new decimal[0];
                            }
                            break;
                        case TypeCode.Int32:
                            if (!reader.IsEmptyElement)
                            {
                                reader.ReadStartElement();
                                value = reader.ReadInt32Array(_itemName, _itemNamespace);
                                reader.ReadEndElement();
                            }
                            else
                            {
                                reader.Read();
                                value = new int[0];
                            }
                            break;
                        case TypeCode.Int64:
                            if (!reader.IsEmptyElement)
                            {
                                reader.ReadStartElement();
                                value = reader.ReadInt64Array(_itemName, _itemNamespace);
                                reader.ReadEndElement();
                            }
                            else
                            {
                                reader.Read();
                                value = new long[0];
                            }
                            break;
                        case TypeCode.Single:
                            if (!reader.IsEmptyElement)
                            {
                                reader.ReadStartElement();
                                value = reader.ReadSingleArray(_itemName, _itemNamespace);
                                reader.ReadEndElement();
                            }
                            else
                            {
                                reader.Read();
                                value = new float[0];
                            }
                            break;
                        case TypeCode.Double:
                            if (!reader.IsEmptyElement)
                            {
                                reader.ReadStartElement();
                                value = reader.ReadDoubleArray(_itemName, _itemNamespace);
                                reader.ReadEndElement();
                            }
                            else
                            {
                                reader.Read();
                                value = new double[0];
                            }
                            break;
                        default:
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxInvalidUseOfPrimitiveOperationFormatter));
                    }
                }
                else
                {
                    switch (_typeCode)
                    {
                        case TypeCode.Boolean:
                            value = reader.ReadElementContentAsBoolean();
                            break;
                        case TypeCode.DateTime:
                            value = reader.ReadElementContentAsDateTime();
                            break;
                        case TypeCode.Decimal:
                            value = reader.ReadElementContentAsDecimal();
                            break;
                        case TypeCode.Double:
                            value = reader.ReadElementContentAsDouble();
                            break;
                        case TypeCode.Int32:
                            value = reader.ReadElementContentAsInt();
                            break;
                        case TypeCode.Int64:
                            value = reader.ReadElementContentAsLong();
                            break;
                        case TypeCode.Single:
                            value = reader.ReadElementContentAsFloat();
                            break;
                        case TypeCode.String:
                            return reader.ReadElementContentAsString();
                        default:
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxInvalidUseOfPrimitiveOperationFormatter));
                    }
                }
                return value;
            }

            public void WriteValue(XmlDictionaryWriter writer, object value)
            {
                if (_isArray)
                {
                    switch (_typeCode)
                    {
                        case TypeCode.Byte:
                            {
                                byte[] arrayValue = (byte[])value;
                                writer.WriteBase64(arrayValue, 0, arrayValue.Length);
                            }
                            break;
                        case TypeCode.Boolean:
                            {
                                bool[] arrayValue = (bool[])value;
                                writer.WriteArray(null, _itemName, _itemNamespace, arrayValue, 0, arrayValue.Length);
                            }
                            break;
                        case TypeCode.DateTime:
                            {
                                DateTime[] arrayValue = (DateTime[])value;
                                writer.WriteArray(null, _itemName, _itemNamespace, arrayValue, 0, arrayValue.Length);
                            }
                            break;
                        case TypeCode.Decimal:
                            {
                                decimal[] arrayValue = (decimal[])value;
                                writer.WriteArray(null, _itemName, _itemNamespace, arrayValue, 0, arrayValue.Length);
                            }
                            break;
                        case TypeCode.Int32:
                            {
                                int[] arrayValue = (int[])value;
                                writer.WriteArray(null, _itemName, _itemNamespace, arrayValue, 0, arrayValue.Length);
                            }
                            break;
                        case TypeCode.Int64:
                            {
                                long[] arrayValue = (long[])value;
                                writer.WriteArray(null, _itemName, _itemNamespace, arrayValue, 0, arrayValue.Length);
                            }
                            break;
                        case TypeCode.Single:
                            {
                                float[] arrayValue = (float[])value;
                                writer.WriteArray(null, _itemName, _itemNamespace, arrayValue, 0, arrayValue.Length);
                            }
                            break;
                        case TypeCode.Double:
                            {
                                double[] arrayValue = (double[])value;
                                writer.WriteArray(null, _itemName, _itemNamespace, arrayValue, 0, arrayValue.Length);
                            }
                            break;
                        default:
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxInvalidUseOfPrimitiveOperationFormatter));
                    }
                }
                else
                {
                    switch (_typeCode)
                    {
                        case TypeCode.Boolean:
                            writer.WriteValue((bool)value);
                            break;
                        case TypeCode.DateTime:
                            writer.WriteValue((DateTime)value);
                            break;
                        case TypeCode.Decimal:
                            writer.WriteValue((decimal)value);
                            break;
                        case TypeCode.Double:
                            writer.WriteValue((double)value);
                            break;
                        case TypeCode.Int32:
                            writer.WriteValue((int)value);
                            break;
                        case TypeCode.Int64:
                            writer.WriteValue((long)value);
                            break;
                        case TypeCode.Single:
                            writer.WriteValue((float)value);
                            break;
                        case TypeCode.String:
                            writer.WriteString((string)value);
                            break;
                        default:
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxInvalidUseOfPrimitiveOperationFormatter));
                    }
                }
            }
        }

        private class PrimitiveRequestBodyWriter : BodyWriter
        {
            private readonly object[] _parameters;
            private readonly PrimitiveOperationFormatter _primitiveOperationFormatter;

            public PrimitiveRequestBodyWriter(object[] parameters, PrimitiveOperationFormatter primitiveOperationFormatter)
                : base(true)
            {
                _parameters = parameters;
                _primitiveOperationFormatter = primitiveOperationFormatter;
            }

            protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
            {
                _primitiveOperationFormatter.SerializeRequest(writer, _parameters);
            }
        }

        private class PrimitiveResponseBodyWriter : BodyWriter
        {
            private readonly object[] _parameters;
            private readonly object _returnValue;
            private readonly PrimitiveOperationFormatter _primitiveOperationFormatter;

            public PrimitiveResponseBodyWriter(object[] parameters, object returnValue,
                PrimitiveOperationFormatter primitiveOperationFormatter)
                : base(true)
            {
                _parameters = parameters;
                _returnValue = returnValue;
                _primitiveOperationFormatter = primitiveOperationFormatter;
            }

            protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
            {
                _primitiveOperationFormatter.SerializeResponse(writer, _returnValue, _parameters);
            }
        }
    }
}