// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class DataContractJsonSerializerOperationFormatter : DataContractSerializerOperationFormatter
    {
        private readonly bool _isBareMessageContractReply;
        private readonly bool _isBareMessageContractRequest;
        // isWrapped is true when the user has explicitly chosen the response or request format to be Wrapped (allowed only in WebHttpBehavior)
        private readonly bool _isWrapped;

        private static readonly DataContractFormatAttribute s_defaultDataContractFormatAttribute = new DataContractFormatAttribute();

        public DataContractJsonSerializerOperationFormatter(OperationDescription description, int maxItemsInObjectGraph, bool ignoreExtensionDataObject, bool isWrapped, bool useAspNetAjaxJson, string callbackParameterName)
            : base(description, s_defaultDataContractFormatAttribute, new DataContractJsonSerializerOperationBehavior(description, maxItemsInObjectGraph, ignoreExtensionDataObject, useAspNetAjaxJson))
        {
            if (requestMessageInfo != null)
            {
                if (requestMessageInfo.WrapperName == null)
                {
                    _isBareMessageContractRequest = true;
                }
                else
                {
                    requestMessageInfo.WrapperName = JsonGlobals.RootDictionaryString;
                    requestMessageInfo.WrapperNamespace = XmlDictionaryString.Empty;
                }
            }

            if (replyMessageInfo != null)
            {
                if (replyMessageInfo.WrapperName == null)
                {
                    _isBareMessageContractReply = true;
                }
                else
                {
                    if (useAspNetAjaxJson)
                    {
                        replyMessageInfo.WrapperName = JsonGlobals.DDictionaryString;
                    }
                    else
                    {
                        replyMessageInfo.WrapperName = JsonGlobals.RootDictionaryString;
                    }
                    replyMessageInfo.WrapperNamespace = XmlDictionaryString.Empty;
                }
            }

            _isWrapped = isWrapped;
        }

        internal static bool IsJsonLocalName(XmlDictionaryReader reader, string elementName)
        {
            if (reader.IsStartElement(JsonGlobals.ItemDictionaryString, JsonGlobals.ItemDictionaryString))
            {
                if (reader.MoveToAttribute(JsonGlobals.ItemString))
                {
                    return (reader.Value == elementName);
                }
            }

            return false;
        }

        internal static bool IsStartElement(XmlDictionaryReader reader, string elementName)
        {
            if (reader.IsStartElement(elementName))
            {
                return true;
            }

            return IsJsonLocalName(reader, elementName);
        }

        internal static bool IsStartElement(XmlDictionaryReader reader, XmlDictionaryString elementName, XmlDictionaryString elementNamespace)
        {
            if (reader.IsStartElement(elementName, elementNamespace))
            {
                return true;
            }

            return IsJsonLocalName(reader, (elementName == null) ? null : elementName.Value);
        }

        protected override void AddHeadersToMessage(Message message, MessageDescription messageDescription, object[] parameters, bool isRequest)
        {
            if (message != null)
            {
                message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.JsonProperty);
            }

            base.AddHeadersToMessage(message, messageDescription, parameters, isRequest);
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

            if (reader.EOF)
            {
                return null;
            }

            if ((isRequest && _isBareMessageContractRequest) || (!isRequest && _isBareMessageContractReply))
            {
                return DeserializeBareMessageContract(reader, parameters, isRequest);
            }

            object returnValue = null;

            if (isRequest || _isWrapped)
            {
                ValidateTypeObjectAttribute(reader, isRequest);
                returnValue = DeserializeBodyCore(reader, parameters, isRequest);
            }
            else
            {
                if (replyMessageInfo.ReturnPart != null)
                {
                    PartInfo part = replyMessageInfo.ReturnPart;
                    DataContractJsonSerializer serializer = part.Serializer as DataContractJsonSerializer;

                    serializer = RecreateDataContractJsonSerializer(serializer, part.ContractType, JsonGlobals.RootString);
                    VerifyIsStartElement(reader, JsonGlobals.RootString);

                    if (serializer.IsStartObject(reader))
                    {
                        try
                        {
                            returnValue = part.ReadObject(reader, serializer);
                        }
                        catch (InvalidOperationException e)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameter, part.Description.Namespace, part.Description.Name), e));
                        }
                        catch (InvalidDataContractException e)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataContractException(
                                SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameter, part.Description.Namespace, part.Description.Name), e));
                        }
                        catch (FormatException e)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                NetDispatcherFaultException.CreateDeserializationFailedFault(
                                SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameterMore,
                                part.Description.Namespace, part.Description.Name, e.Message),
                                e));
                        }
                        catch (SerializationException e)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                NetDispatcherFaultException.CreateDeserializationFailedFault(
                                SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameterMore,
                                part.Description.Namespace, part.Description.Name, e.Message),
                                e));
                        }
                    }
                }
                else if (replyMessageInfo.BodyParts != null)
                {
                    ValidateTypeObjectAttribute(reader, isRequest);
                    returnValue = DeserializeBodyCore(reader, parameters, isRequest);
                }

                while (reader.IsStartElement())
                {
                    TraceAndSkipElement(reader);
                }
            }

            return returnValue;
        }

        protected override void GetHeadersFromMessage(Message message, MessageDescription messageDescription, object[] parameters, bool isRequest)
        {
            if (message != null)
            {
                message.Properties.TryGetValue(WebBodyFormatMessageProperty.Name, out object prop);
                WebBodyFormatMessageProperty formatProperty = (prop as WebBodyFormatMessageProperty);
                if (formatProperty == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MessageFormatPropertyNotFound2, OperationName)));
                }
                if (formatProperty.Format != WebContentFormat.Json)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InvalidHttpMessageFormat3, OperationName, formatProperty.Format, WebContentFormat.Json)));
                }
            }

            base.GetHeadersFromMessage(message, messageDescription, parameters, isRequest);
        }

        protected override void SerializeBody(XmlDictionaryWriter writer, MessageVersion version, string action, MessageDescription messageDescription, object returnValue, object[] parameters, bool isRequest)
        {
            if ((isRequest && _isBareMessageContractRequest) || (!isRequest && _isBareMessageContractReply))
            {
                SerializeBareMessageContract(writer, parameters, isRequest);
            }
            else
            {
                if (isRequest || _isWrapped)
                {
                    SerializeBody(writer, returnValue, parameters, isRequest);
                }
                else
                {
                    if (replyMessageInfo.ReturnPart != null)
                    {
                        DataContractJsonSerializer serializer = replyMessageInfo.ReturnPart.Serializer as DataContractJsonSerializer;
                        serializer = RecreateDataContractJsonSerializer(serializer, replyMessageInfo.ReturnPart.ContractType, JsonGlobals.RootString);

                        try
                        {
                            serializer.WriteObject(writer, returnValue);
                        }
                        catch (SerializationException sx)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                                SR.Format(SR.SFxInvalidMessageBodyErrorSerializingParameter, replyMessageInfo.ReturnPart.Description.Namespace, replyMessageInfo.ReturnPart.Description.Name, sx.Message), sx));
                        }
                    }
                    else if (replyMessageInfo.BodyParts != null)
                    {
                        SerializeBody(writer, returnValue, parameters, isRequest);
                    }
                }
            }
        }

        private static DataContractJsonSerializer RecreateDataContractJsonSerializer(DataContractJsonSerializer serializer, Type type, string newRootName)
        {
            DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings
            {
                RootName = newRootName,
                KnownTypes = serializer.KnownTypes,
                MaxItemsInObjectGraph = serializer.MaxItemsInObjectGraph,
                IgnoreExtensionDataObject = serializer.IgnoreExtensionDataObject,
                EmitTypeInformation = serializer.EmitTypeInformation,
                DateTimeFormat = serializer.DateTimeFormat,
                UseSimpleDictionaryFormat = serializer.UseSimpleDictionaryFormat
            };

            return new DataContractJsonSerializer(type, settings);
        }

        private object DeserializeBareMessageContract(XmlDictionaryReader reader, object[] parameters, bool isRequest)
        {
            MessageInfo messageInfo;
            if (isRequest)
            {
                messageInfo = requestMessageInfo;
            }
            else
            {
                messageInfo = replyMessageInfo;
            }

            if (messageInfo.BodyParts.Length > 0)
            {
                PartInfo part = messageInfo.BodyParts[0];
                DataContractJsonSerializer serializer = part.Serializer as DataContractJsonSerializer;
                serializer = RecreateDataContractJsonSerializer(serializer, part.ContractType, JsonGlobals.RootString);

                while (reader.IsStartElement())
                {
                    if (serializer.IsStartObject(reader))
                    {
                        try
                        {
                            parameters[part.Description.Index] = part.ReadObject(reader, serializer);
                            break;
                        }
                        catch (InvalidOperationException e)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                                SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameter, part.Description.Namespace, part.Description.Name), e));
                        }
                        catch (InvalidDataContractException e)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataContractException(
                                SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameter, part.Description.Namespace, part.Description.Name), e));
                        }
                        catch (FormatException e)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                NetDispatcherFaultException.CreateDeserializationFailedFault(
                                SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameterMore,
                                part.Description.Namespace, part.Description.Name, e.Message),
                                e));
                        }
                        catch (SerializationException e)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                NetDispatcherFaultException.CreateDeserializationFailedFault(
                                SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameterMore,
                                part.Description.Namespace, part.Description.Name, e.Message),
                                e));
                        }
                    }
                    else
                    {
                        TraceAndSkipElement(reader);
                    }
                }
                while (reader.IsStartElement())
                {
                    TraceAndSkipElement(reader);
                }
            }

            return null;
        }

        private object DeserializeBodyCore(XmlDictionaryReader reader, object[] parameters, bool isRequest)
        {
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
                VerifyIsStartElement(reader, messageInfo.WrapperName, messageInfo.WrapperNamespace);
                bool isEmptyElement = reader.IsEmptyElement;
                reader.Read();
                if (isEmptyElement)
                {
                    return null;
                }
            }

            object returnValue = null;
            DeserializeParameters(reader, messageInfo.BodyParts, parameters, messageInfo.ReturnPart, ref returnValue);
            if (messageInfo.WrapperName != null)
            {
                reader.ReadEndElement();
            }

            return returnValue;
        }

        private object DeserializeParameter(XmlDictionaryReader reader, PartInfo part)
        {
            if (part.Description.Multiple)
            {
                ArrayList items = new ArrayList();
                while (part.Serializer.IsStartObject(reader))
                {
                    items.Add(DeserializeParameterPart(reader, part));
                }

                return items.ToArray(part.Description.Type);
            }

            return DeserializeParameterPart(reader, part);
        }

        private object DeserializeParameterPart(XmlDictionaryReader reader, PartInfo part)
        {
            object val;
            try
            {
                val = part.ReadObject(reader);
            }
            catch (InvalidOperationException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameter, part.Description.Namespace, part.Description.Name), e));
            }
            catch (InvalidDataContractException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataContractException(
                    SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameter, part.Description.Namespace, part.Description.Name), e));
            }
            catch (FormatException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    NetDispatcherFaultException.CreateDeserializationFailedFault(
                    SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameterMore,
                    part.Description.Namespace, part.Description.Name, e.Message),
                    e));
            }
            catch (SerializationException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    NetDispatcherFaultException.CreateDeserializationFailedFault(
                    SR.Format(SR.SFxInvalidMessageBodyErrorDeserializingParameterMore,
                    part.Description.Namespace, part.Description.Name, e.Message),
                    e));
            }

            return val;
        }

        private void DeserializeParameters(XmlDictionaryReader reader, PartInfo[] parts, object[] parameters, PartInfo returnInfo, ref object returnValue)
        {
            bool[] setParameters = new bool[parameters.Length];
            bool hasReadReturnValue = false;
            int currentIndex = 0;

            while (reader.IsStartElement())
            {
                bool hasReadParameter = false;

                for (int i = 0, index = currentIndex; i < parts.Length; i++, index = (index + 1) % parts.Length)
                {
                    PartInfo part = parts[index];
                    if (part.Serializer.IsStartObject(reader))
                    {
                        currentIndex = i;
                        parameters[part.Description.Index] = DeserializeParameter(reader, part);
                        setParameters[part.Description.Index] = true;
                        hasReadParameter = true;
                    }
                }

                if (!hasReadParameter)
                {
                    if ((returnInfo != null) && !hasReadReturnValue && returnInfo.Serializer.IsStartObject(reader))
                    {
                        returnValue = DeserializeParameter(reader, returnInfo);
                        hasReadReturnValue = true;
                    }
                    else
                    {
                        TraceAndSkipElement(reader);
                    }
                }
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                if (!setParameters[i])
                {
                    parameters[i] = null;
                }
            }
        }

        private void SerializeBareMessageContract(XmlDictionaryWriter writer, object[] parameters, bool isRequest)
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

            if (messageInfo.BodyParts.Length > 0)
            {
                PartInfo part = messageInfo.BodyParts[0];
                DataContractJsonSerializer serializer = part.Serializer as DataContractJsonSerializer;
                serializer = RecreateDataContractJsonSerializer(serializer, part.ContractType, JsonGlobals.RootString);

                object graph = parameters[part.Description.Index];
                try
                {
                    serializer.WriteObject(writer, graph);
                }
                catch (SerializationException sx)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                        SR.Format(SR.SFxInvalidMessageBodyErrorSerializingParameter, part.Description.Namespace, part.Description.Name, sx.Message), sx));
                }
            }
        }

        private void SerializeBody(XmlDictionaryWriter writer, object returnValue, object[] parameters, bool isRequest)
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
                writer.WriteAttributeString(JsonGlobals.TypeString, JsonGlobals.ObjectString);
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

        private void SerializeParameters(XmlDictionaryWriter writer, PartInfo[] parts, object[] parameters)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                PartInfo part = parts[i];
                object graph = parameters[part.Description.Index];
                SerializeParameter(writer, part, graph);
            }
        }

        private void ValidateTypeObjectAttribute(XmlDictionaryReader reader, bool isRequest)
        {
            MessageInfo messageInfo = isRequest ? requestMessageInfo : replyMessageInfo;
            if (messageInfo.WrapperName != null)
            {
                if (!IsStartElement(reader, messageInfo.WrapperName, messageInfo.WrapperNamespace))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.Format(SR.SFxInvalidMessageBody, messageInfo.WrapperName, messageInfo.WrapperNamespace, reader.NodeType, reader.Name, reader.NamespaceURI)));
                }

                string typeAttribute = reader.GetAttribute(JsonGlobals.TypeString);
                if (!typeAttribute.Equals(JsonGlobals.ObjectString, StringComparison.Ordinal))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new SerializationException(SR.Format(SR.JsonFormatterExpectedAttributeObject, typeAttribute)));
                }
            }
        }

        private void VerifyIsStartElement(XmlDictionaryReader reader, string elementName)
        {
            bool foundElement = false;
            while (reader.IsStartElement())
            {
                if (IsStartElement(reader, elementName))
                {
                    foundElement = true;
                    break;
                }
                else
                {
                    TraceAndSkipElement(reader);
                }
            }

            if (!foundElement)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.Format(SR.SFxInvalidMessageBody, elementName, string.Empty, reader.NodeType, reader.Name, reader.NamespaceURI)));
            }
        }

        private void VerifyIsStartElement(XmlDictionaryReader reader, XmlDictionaryString elementName, XmlDictionaryString elementNamespace)
        {
            bool foundElement = false;
            while (reader.IsStartElement())
            {
                if (IsStartElement(reader, elementName, elementNamespace))
                {
                    foundElement = true;
                    break;
                }
                else
                {
                    TraceAndSkipElement(reader);
                }
            }

            if (!foundElement)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SerializationException(SR.Format(SR.SFxInvalidMessageBody, elementName, elementNamespace, reader.NodeType, reader.Name, reader.NamespaceURI)));
            }
        }

        private void WriteVoidReturn(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement(JsonGlobals.DString);
            writer.WriteAttributeString(JsonGlobals.TypeString, JsonGlobals.NullString);
            writer.WriteEndElement();
        }


        private static void TraceAndSkipElement(XmlReader xmlReader)
        {
            //if (DiagnosticUtility.ShouldTraceVerbose)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Verbose, TraceCode.ElementIgnored, SR.SFxTraceCodeElementIgnored, new StringTraceRecord("Element", xmlReader.NamespaceURI + ":" + xmlReader.LocalName));
            //}
            xmlReader.Skip();
        }
    }
}
