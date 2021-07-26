// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    internal abstract class OperationFormatter : IClientMessageFormatter, IDispatchMessageFormatter
    {
        private readonly XmlDictionaryString _action;
        private readonly XmlDictionaryString _replyAction;
        protected StreamFormatter requestStreamFormatter, replyStreamFormatter;

        public OperationFormatter(OperationDescription description, bool isRpc, bool isEncoded)
        {
            Validate(description, isRpc, isEncoded);
            RequestDescription = description.Messages[0];
            if (description.Messages.Count == 2)
            {
                ReplyDescription = description.Messages[1];
            }

            int stringCount = 3 + RequestDescription.Body.Parts.Count;
            if (ReplyDescription != null)
            {
                stringCount += 2 + ReplyDescription.Body.Parts.Count;
            }

            Dictionary = new XmlDictionary(stringCount * 2);
            GetActions(description, Dictionary, out _action, out _replyAction);
            OperationName = description.Name;
            requestStreamFormatter = StreamFormatter.Create(RequestDescription, OperationName, true/*isRequest*/);
            if (ReplyDescription != null)
            {
                replyStreamFormatter = StreamFormatter.Create(ReplyDescription, OperationName, false/*isResponse*/);
            }
        }

        protected abstract void AddHeadersToMessage(Message message, MessageDescription messageDescription, object[] parameters, bool isRequest);
        protected abstract void SerializeBody(XmlDictionaryWriter writer, MessageVersion version, string action, MessageDescription messageDescription, object returnValue, object[] parameters, bool isRequest);
        protected abstract void GetHeadersFromMessage(Message message, MessageDescription messageDescription, object[] parameters, bool isRequest);
        protected abstract object DeserializeBody(XmlDictionaryReader reader, MessageVersion version, string action, MessageDescription messageDescription, object[] parameters, bool isRequest);

        protected virtual void WriteBodyAttributes(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
        }

        internal string RequestAction
        {
            get
            {
                if (_action != null)
                {
                    return _action.Value;
                }

                return null;
            }
        }
        internal string ReplyAction
        {
            get
            {
                if (_replyAction != null)
                {
                    return _replyAction.Value;
                }

                return null;
            }
        }

        protected XmlDictionary Dictionary { get; }

        protected string OperationName { get; }

        protected MessageDescription ReplyDescription { get; }

        protected MessageDescription RequestDescription { get; }

        protected XmlDictionaryString AddToDictionary(string s)
        {
            return AddToDictionary(Dictionary, s);
        }

        public object DeserializeReply(Message message, object[] parameters)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            if (parameters == null)
            {
                throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(parameters)), message);
            }

            try
            {
                object result = null;
                if (ReplyDescription.IsTypedMessage)
                {
                    object typeMessageInstance = CreateTypedMessageInstance(ReplyDescription.MessageType);
                    TypedMessageParts typedMessageParts = new TypedMessageParts(typeMessageInstance, ReplyDescription);
                    object[] parts = new object[typedMessageParts.Count];

                    GetPropertiesFromMessage(message, ReplyDescription, parts);
                    GetHeadersFromMessage(message, ReplyDescription, parts, false/*isRequest*/);
                    DeserializeBodyContents(message, parts, false/*isRequest*/);

                    // copy values into the actual field/properties
                    typedMessageParts.SetTypedMessageParts(parts);

                    result = typeMessageInstance;
                }
                else
                {
                    GetPropertiesFromMessage(message, ReplyDescription, parameters);
                    GetHeadersFromMessage(message, ReplyDescription, parameters, false/*isRequest*/);
                    result = DeserializeBodyContents(message, parameters, false/*isRequest*/);
                }
                return result;
            }
            catch (XmlException xe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.Format(SR.SFxErrorDeserializingReplyBodyMore, OperationName, xe.Message), xe));
            }
            catch (FormatException fe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.Format(SR.SFxErrorDeserializingReplyBodyMore, OperationName, fe.Message), fe));
            }
            catch (SerializationException se)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.Format(SR.SFxErrorDeserializingReplyBodyMore, OperationName, se.Message), se));
            }
        }

        private static object CreateTypedMessageInstance(Type messageContractType)
        {
            try
            {
                object typeMessageInstance = Activator.CreateInstance(messageContractType);
                return typeMessageInstance;
            }
            catch (MissingMethodException mme)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxMessageContractRequiresDefaultConstructor, messageContractType.FullName), mme));
            }
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            if (parameters == null)
            {
                throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(parameters)), message);
            }

            try
            {
                if (RequestDescription.IsTypedMessage)
                {
                    object typeMessageInstance = CreateTypedMessageInstance(RequestDescription.MessageType);
                    TypedMessageParts typedMessageParts = new TypedMessageParts(typeMessageInstance, RequestDescription);
                    object[] parts = new object[typedMessageParts.Count];

                    GetPropertiesFromMessage(message, RequestDescription, parts);
                    GetHeadersFromMessage(message, RequestDescription, parts, true/*isRequest*/);
                    DeserializeBodyContents(message, parts, true/*isRequest*/);

                    // copy values into the actual field/properties
                    typedMessageParts.SetTypedMessageParts(parts);

                    parameters[0] = typeMessageInstance;
                }
                else
                {
                    GetPropertiesFromMessage(message, RequestDescription, parameters);
                    GetHeadersFromMessage(message, RequestDescription, parameters, true/*isRequest*/);
                    DeserializeBodyContents(message, parameters, true/*isRequest*/);
                }
            }
            catch (XmlException xe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    CreateDeserializationFailedFault(
                        SR.Format(SR.SFxErrorDeserializingRequestBodyMore, OperationName, xe.Message),
                        xe));
            }
            catch (FormatException fe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    CreateDeserializationFailedFault(
                        SR.Format(SR.SFxErrorDeserializingRequestBodyMore, OperationName, fe.Message),
                        fe));
            }
            catch (SerializationException se)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.Format(SR.SFxErrorDeserializingRequestBodyMore, OperationName, se.Message),
                    se));
            }
        }

        private object DeserializeBodyContents(Message message, object[] parameters, bool isRequest)
        {
            SetupStreamAndMessageDescription(isRequest, out StreamFormatter streamFormatter, out MessageDescription messageDescription);

            if (streamFormatter != null)
            {
                object retVal = null;
                streamFormatter.Deserialize(parameters, ref retVal, message);
                return retVal;
            }

            if (message.IsEmpty)
            {
                return null;
            }
            else
            {
                XmlDictionaryReader reader = message.GetReaderAtBodyContents();
                using (reader)
                {
                    object body = DeserializeBody(reader, message.Version, RequestAction, messageDescription, parameters, isRequest);
                    message.ReadFromBodyContentsToEnd(reader);
                    return body;
                }
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

            object[] parts;
            if (RequestDescription.IsTypedMessage)
            {
                TypedMessageParts typedMessageParts = new TypedMessageParts(parameters[0], RequestDescription);

                // copy values from the actual field/properties
                parts = new object[typedMessageParts.Count];
                typedMessageParts.GetTypedMessageParts(parts);
            }
            else
            {
                parts = parameters;
            }
            Message msg = new OperationFormatterMessage(this, messageVersion,
                _action == null ? null : ActionHeader.Create(_action, messageVersion.Addressing),
                parts, null, true/*isRequest*/);
            AddPropertiesToMessage(msg, RequestDescription, parts);
            AddHeadersToMessage(msg, RequestDescription, parts, true /*isRequest*/);

            return msg;
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

            object[] parts;
            object resultPart;
            if (ReplyDescription.IsTypedMessage)
            {
                // If the response is a typed message then it must 
                // me the response (as opposed to an out param).  We will
                // serialize the response in the exact same way that we
                // would serialize a bunch of outs (with no return value).

                TypedMessageParts typedMessageParts = new TypedMessageParts(result, ReplyDescription);

                // make a copy of the list so that we have the actual values of the field/properties
                parts = new object[typedMessageParts.Count];
                typedMessageParts.GetTypedMessageParts(parts);

                resultPart = null;
            }
            else
            {
                parts = parameters;
                resultPart = result;
            }

            Message msg = new OperationFormatterMessage(this, messageVersion,
                _replyAction == null ? null : ActionHeader.Create(_replyAction, messageVersion.Addressing),
                parts, resultPart, false/*isRequest*/);
            AddPropertiesToMessage(msg, ReplyDescription, parts);
            AddHeadersToMessage(msg, ReplyDescription, parts, false /*isRequest*/);
            return msg;
        }

        private void SetupStreamAndMessageDescription(bool isRequest, out StreamFormatter streamFormatter, out MessageDescription messageDescription)
        {
            if (isRequest)
            {
                streamFormatter = requestStreamFormatter;
                messageDescription = RequestDescription;
            }
            else
            {
                streamFormatter = replyStreamFormatter;
                messageDescription = ReplyDescription;
            }
        }

        private async Task SerializeBodyContentsAsync(XmlDictionaryWriter writer, MessageVersion version, object[] parameters, object returnValue, bool isRequest)
        {
            SetupStreamAndMessageDescription(isRequest, out StreamFormatter streamFormatter, out MessageDescription messageDescription);

            if (streamFormatter != null)
            {
                await streamFormatter.SerializeAsync(writer, parameters, returnValue);
                return;
            }

            SerializeBody(writer, version, RequestAction, messageDescription, returnValue, parameters, isRequest);
        }

        private void SerializeBodyContents(XmlDictionaryWriter writer, MessageVersion version, object[] parameters, object returnValue, bool isRequest)
        {
            SetupStreamAndMessageDescription(isRequest, out StreamFormatter streamFormatter, out MessageDescription messageDescription);

            if (streamFormatter != null)
            {
                streamFormatter.Serialize(writer, parameters, returnValue);
                return;
            }

            SerializeBody(writer, version, RequestAction, messageDescription, returnValue, parameters, isRequest);
        }

        private void AddPropertiesToMessage(Message message, MessageDescription messageDescription, object[] parameters)
        {
            if (messageDescription.Properties.Count > 0)
            {
                AddPropertiesToMessageCore(message, messageDescription, parameters);
            }
        }

        private void AddPropertiesToMessageCore(Message message, MessageDescription messageDescription, object[] parameters)
        {
            MessageProperties properties = message.Properties;
            MessagePropertyDescriptionCollection propertyDescriptions = messageDescription.Properties;
            for (int i = 0; i < propertyDescriptions.Count; i++)
            {
                MessagePropertyDescription propertyDescription = propertyDescriptions[i];
                object parameter = parameters[propertyDescription.Index];
                if (null != parameter)
                {
                    properties.Add(propertyDescription.Name, parameter);
                }
            }
        }

        private void GetPropertiesFromMessage(Message message, MessageDescription messageDescription, object[] parameters)
        {
            if (messageDescription.Properties.Count > 0)
            {
                GetPropertiesFromMessageCore(message, messageDescription, parameters);
            }
        }

        private void GetPropertiesFromMessageCore(Message message, MessageDescription messageDescription, object[] parameters)
        {
            MessageProperties properties = message.Properties;
            MessagePropertyDescriptionCollection propertyDescriptions = messageDescription.Properties;
            for (int i = 0; i < propertyDescriptions.Count; i++)
            {
                MessagePropertyDescription propertyDescription = propertyDescriptions[i];
                if (properties.ContainsKey(propertyDescription.Name))
                {
                    parameters[propertyDescription.Index] = properties[propertyDescription.Name];
                }
            }
        }

        internal static object GetContentOfMessageHeaderOfT(MessageHeaderDescription headerDescription, object parameterValue, out bool mustUnderstand, out bool relay, out string actor)
        {
            actor = headerDescription.Actor;
            mustUnderstand = headerDescription.MustUnderstand;
            relay = headerDescription.Relay;

            if (headerDescription.TypedHeader && parameterValue != null)
            {
                parameterValue = TypedHeaderManager.GetContent(headerDescription.Type, parameterValue, out mustUnderstand, out relay, out actor);
            }

            return parameterValue;
        }

        internal static bool IsValidReturnValue(MessagePartDescription returnValue)
        {
            return (returnValue != null) && (returnValue.Type != typeof(void));
        }

        internal static XmlDictionaryString AddToDictionary(XmlDictionary dictionary, string s)
        {
            if (!dictionary.TryLookup(s, out XmlDictionaryString dictionaryString))
            {
                dictionaryString = dictionary.Add(s);
            }
            return dictionaryString;
        }

        internal static void Validate(OperationDescription operation, bool isRpc, bool isEncoded)
        {
            if (isEncoded && !isRpc)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxDocEncodedNotSupported, operation.Name)));
            }

            bool hasVoid = false;
            bool hasTypedOrUntypedMessage = false;
            bool hasParameter = false;
            for (int i = 0; i < operation.Messages.Count; i++)
            {
                MessageDescription message = operation.Messages[i];
                if (message.IsTypedMessage || message.IsUntypedMessage)
                {
                    if (isRpc && operation.IsValidateRpcWrapperName)
                    {
                        if (!isEncoded)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxTypedMessageCannotBeRpcLiteral, operation.Name)));
                        }
                    }
                    hasTypedOrUntypedMessage = true;
                }
                else if (message.IsVoid)
                {
                    hasVoid = true;
                }
                else
                {
                    hasParameter = true;
                }
            }
            if (hasParameter && hasTypedOrUntypedMessage)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxTypedOrUntypedMessageCannotBeMixedWithParameters, operation.Name)));
            }

            if (isRpc && hasTypedOrUntypedMessage && hasVoid)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxTypedOrUntypedMessageCannotBeMixedWithVoidInRpc, operation.Name)));
            }
        }

        internal static void GetActions(OperationDescription description, XmlDictionary dictionary, out XmlDictionaryString action, out XmlDictionaryString replyAction)
        {
            string actionString, replyActionString;
            actionString = description.Messages[0].Action;
            if (actionString == MessageHeaders.WildcardAction)
            {
                actionString = null;
            }

            if (!description.IsOneWay)
            {
                replyActionString = description.Messages[1].Action;
            }
            else
            {
                replyActionString = null;
            }

            if (replyActionString == MessageHeaders.WildcardAction)
            {
                replyActionString = null;
            }

            action = replyAction = null;
            if (actionString != null)
            {
                action = AddToDictionary(dictionary, actionString);
            }

            if (replyActionString != null)
            {
                replyAction = AddToDictionary(dictionary, replyActionString);
            }
        }

        internal static NetDispatcherFaultException CreateDeserializationFailedFault(string reason, Exception innerException)
        {
            reason = SR.Format(SR.SFxDeserializationFailed1, reason);
            FaultCode code = new FaultCode(FaultCodeConstants.Codes.DeserializationFailed, FaultCodeConstants.Namespaces.NetDispatch);
            code = FaultCode.CreateSenderFaultCode(code);
            return new NetDispatcherFaultException(reason, code, innerException);
        }

        internal static void TraceAndSkipElement(XmlReader xmlReader)
        {
            //if (DiagnosticUtility.ShouldTraceVerbose)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Verbose, TraceCode.ElementIgnored, SR.SFxTraceCodeElementIgnored, new StringTraceRecord("Element", xmlReader.NamespaceURI + ":" + xmlReader.LocalName));
            //}
            xmlReader.Skip();
        }

        private class TypedMessageParts
        {
            private readonly object _instance;
            private readonly MemberInfo[] _members;

            public TypedMessageParts(object instance, MessageDescription description)
            {
                if (description == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(description)));
                }

                _members = new MemberInfo[description.Body.Parts.Count + description.Properties.Count + description.Headers.Count];

                foreach (MessagePartDescription part in description.Headers)
                {
                    _members[part.Index] = part.MemberInfo;
                }

                foreach (MessagePartDescription part in description.Properties)
                {
                    _members[part.Index] = part.MemberInfo;
                }

                foreach (MessagePartDescription part in description.Body.Parts)
                {
                    _members[part.Index] = part.MemberInfo;
                }

                _instance = instance ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(SR.Format(SR.SFxTypedMessageCannotBeNull, description.Action)));
            }

            private object GetValue(int index)
            {
                MemberInfo memberInfo = _members[index];
                PropertyInfo propertyInfo = memberInfo as PropertyInfo;
                if (propertyInfo != null)
                {
                    return propertyInfo.GetValue(_instance, null);
                }
                else
                {
                    return ((FieldInfo)memberInfo).GetValue(_instance);
                }
            }

            private void SetValue(object value, int index)
            {
                MemberInfo memberInfo = _members[index];
                PropertyInfo propertyInfo = memberInfo as PropertyInfo;
                if (propertyInfo != null)
                {
                    propertyInfo.SetValue(_instance, value, null);
                }
                else
                {
                    ((FieldInfo)memberInfo).SetValue(_instance, value);
                }
            }

            internal void GetTypedMessageParts(object[] values)
            {
                for (int i = 0; i < _members.Length; i++)
                {
                    values[i] = GetValue(i);
                }
            }

            internal void SetTypedMessageParts(object[] values)
            {
                for (int i = 0; i < _members.Length; i++)
                {
                    SetValue(values[i], i);
                }
            }

            internal int Count
            {
                get { return _members.Length; }
            }
        }

        internal class OperationFormatterMessage : BodyWriterMessage
        {
            private readonly OperationFormatter _operationFormatter;
            public OperationFormatterMessage(OperationFormatter operationFormatter, MessageVersion version, ActionHeader action,
               object[] parameters, object returnValue, bool isRequest)
                : base(version, action, new OperationFormatterBodyWriter(operationFormatter, version, parameters, returnValue, isRequest))
            {
                _operationFormatter = operationFormatter;
            }


            public OperationFormatterMessage(MessageVersion version, string action, BodyWriter bodyWriter) : base(version, action, bodyWriter) { }

            private OperationFormatterMessage(MessageHeaders headers, KeyValuePair<string, object>[] properties, OperationFormatterBodyWriter bodyWriter)
                : base(headers, properties, bodyWriter)
            {
                _operationFormatter = bodyWriter.OperationFormatter;
            }

            protected override void OnWriteStartBody(XmlDictionaryWriter writer)
            {
                base.OnWriteStartBody(writer);
                _operationFormatter.WriteBodyAttributes(writer, Version);
            }

            protected override MessageBuffer OnCreateBufferedCopy(int maxBufferSize)
            {
                BodyWriter bufferedBodyWriter;
                if (BodyWriter.IsBuffered)
                {
                    bufferedBodyWriter = BodyWriter;
                }
                else
                {
                    bufferedBodyWriter = BodyWriter.CreateBufferedCopy(maxBufferSize);
                }
                KeyValuePair<string, object>[] properties = new KeyValuePair<string, object>[base.Properties.Count];
                ((ICollection<KeyValuePair<string, object>>)base.Properties).CopyTo(properties, 0);
                return new OperationFormatterMessageBuffer(base.Headers, properties, bufferedBodyWriter);
            }

            private class OperationFormatterBodyWriter : BodyWriter
            {
                private readonly bool _isRequest;
                private readonly object[] _parameters;
                private readonly object _returnValue;
                private readonly MessageVersion _version;

                public OperationFormatterBodyWriter(OperationFormatter operationFormatter, MessageVersion version,
                    object[] parameters, object returnValue, bool isRequest)
                    : base(AreParametersBuffered(isRequest, operationFormatter))
                {
                    _parameters = parameters;
                    _returnValue = returnValue;
                    _isRequest = isRequest;
                    OperationFormatter = operationFormatter;
                    _version = version;
                }

                private object ThisLock
                {
                    get { return this; }
                }

                private static bool AreParametersBuffered(bool isRequest, OperationFormatter operationFormatter)
                {
                    StreamFormatter streamFormatter = isRequest ? operationFormatter.requestStreamFormatter : operationFormatter.replyStreamFormatter;
                    return streamFormatter == null;
                }

                protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
                {
                    lock (ThisLock)
                    {
                        OperationFormatter.SerializeBodyContents(writer, _version, _parameters, _returnValue, _isRequest);
                    }
                }

                protected override Task OnWriteBodyContentsAsync(XmlDictionaryWriter writer)
                {
                    return OperationFormatter.SerializeBodyContentsAsync(writer, _version, _parameters, _returnValue, _isRequest);
                }

                internal OperationFormatter OperationFormatter { get; }
            }

            private class OperationFormatterMessageBuffer : BodyWriterMessageBuffer
            {
                public OperationFormatterMessageBuffer(MessageHeaders headers,
                    KeyValuePair<string, object>[] properties, BodyWriter bodyWriter)
                    : base(headers, properties, bodyWriter)
                {
                }

                public override Message CreateMessage()
                {
                    if (!(BodyWriter is OperationFormatterBodyWriter operationFormatterBodyWriter))
                    {
                        return base.CreateMessage();
                    }

                    lock (ThisLock)
                    {
                        if (Closed)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                        }

                        return new OperationFormatterMessage(Headers, Properties, operationFormatterBodyWriter);
                    }
                }
            }
        }

        internal abstract class OperationFormatterHeader : MessageHeader
        {
            private readonly MessageHeader _innerHeader; //use innerHeader to handle versionSupported, actor/role handling etc.

            public OperationFormatterHeader(OperationFormatter operationFormatter, MessageVersion version, string name, string ns, bool mustUnderstand, string actor, bool relay)
            {
                if (actor != null)
                {
                    _innerHeader = CreateHeader(name, ns, null/*headerValue*/, mustUnderstand, actor, relay);
                }
                else
                {
                    _innerHeader = CreateHeader(name, ns, null/*headerValue*/, mustUnderstand, "", relay);
                }
            }


            public override bool IsMessageVersionSupported(MessageVersion messageVersion)
            {
                return _innerHeader.IsMessageVersionSupported(messageVersion);
            }


            public override string Name
            {
                get { return _innerHeader.Name; }
            }

            public override string Namespace
            {
                get { return _innerHeader.Namespace; }
            }

            public override bool MustUnderstand
            {
                get { return _innerHeader.MustUnderstand; }
            }

            public override bool Relay
            {
                get { return _innerHeader.Relay; }
            }

            public override string Actor
            {
                get { return _innerHeader.Actor; }
            }

            protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                //Prefix needed since there may be xsi:type attribute at toplevel with qname value where ns = ""
                writer.WriteStartElement((Namespace == null || Namespace.Length == 0) ? string.Empty : "h", Name, Namespace);
                OnWriteHeaderAttributes(writer, messageVersion);
            }

            protected virtual void OnWriteHeaderAttributes(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                WriteHeaderAttributes(writer, messageVersion);
            }
        }

        internal class XmlElementMessageHeader : OperationFormatterHeader
        {
            protected XmlElement headerValue;
            public XmlElementMessageHeader(OperationFormatter operationFormatter, MessageVersion version, string name, string ns, bool mustUnderstand, string actor, bool relay, XmlElement headerValue) :
                base(operationFormatter, version, name, ns, mustUnderstand, actor, relay)
            {
                this.headerValue = headerValue;
            }

            protected override void OnWriteHeaderAttributes(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                WriteHeaderAttributes(writer, messageVersion);
                XmlDictionaryReader nodeReader = XmlDictionaryReader.CreateDictionaryReader(new XmlNodeReader(headerValue));
                nodeReader.MoveToContent();
                writer.WriteAttributes(nodeReader, false);
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                headerValue.WriteContentTo(writer);
            }
        }
        internal struct QName
        {
            internal string Name;
            internal string Namespace;
            internal QName(string name, string ns)
            {
                Name = name;
                Namespace = ns;
            }
        }
        internal class QNameComparer : IEqualityComparer<QName>
        {
            internal static QNameComparer Singleton = new QNameComparer();

            private QNameComparer() { }

            public bool Equals(QName x, QName y)
            {
                return x.Name == y.Name && x.Namespace == y.Namespace;
            }

            public int GetHashCode(QName obj)
            {
                return obj.Name.GetHashCode();
            }
        }
        internal class MessageHeaderDescriptionTable : Dictionary<QName, MessageHeaderDescription>
        {
            internal MessageHeaderDescriptionTable() : base(QNameComparer.Singleton) { }
            internal void Add(string name, string ns, MessageHeaderDescription message)
            {
                Add(new QName(name, ns), message);
            }
            internal MessageHeaderDescription Get(string name, string ns)
            {
                if (TryGetValue(new QName(name, ns), out MessageHeaderDescription message))
                {
                    return message;
                }

                return null;
            }
        }
    }
}
