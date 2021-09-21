// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Description
{
    public class XmlSerializerOperationBehavior : IOperationBehavior, IWsdlExportExtension
    {
        public XmlSerializerOperationBehavior(OperationDescription operation)
            : this(operation, null)
        {
        }

        public XmlSerializerOperationBehavior(OperationDescription operation, XmlSerializerFormatAttribute attribute)
        {
            if (operation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operation));
            }

            Reflector parentReflector = new Reflector(operation.DeclaringContract.Namespace, operation.DeclaringContract.ContractType);
            OperationReflector = parentReflector.ReflectOperation(operation, attribute ?? new XmlSerializerFormatAttribute());
        }

        internal XmlSerializerOperationBehavior(OperationDescription operation, XmlSerializerFormatAttribute attribute, Reflector parentReflector)
            : this(operation, attribute)
        {
            // used by System.ServiceModel.Web
            OperationReflector = parentReflector.ReflectOperation(operation, attribute ?? new XmlSerializerFormatAttribute());
        }

        private XmlSerializerOperationBehavior(Reflector.OperationReflector reflector, bool builtInOperationBehavior)
        {
            Fx.Assert(reflector != null, "");
            OperationReflector = reflector;
            IsBuiltInOperationBehavior = builtInOperationBehavior;
        }

        internal Reflector.OperationReflector OperationReflector { get; }

        internal bool IsBuiltInOperationBehavior { get; }

        public XmlSerializerFormatAttribute XmlSerializerFormatAttribute
        {
            get
            {
                return OperationReflector.Attribute;
            }
        }

        internal static XmlSerializerOperationFormatter CreateOperationFormatter(OperationDescription operation)
        {
            return new XmlSerializerOperationBehavior(operation).CreateFormatter();
        }

        internal static XmlSerializerOperationFormatter CreateOperationFormatter(OperationDescription operation, XmlSerializerFormatAttribute attr)
        {
            return new XmlSerializerOperationBehavior(operation, attr).CreateFormatter();
        }

        internal static void AddBehaviors(ContractDescription contract)
        {
            AddBehaviors(contract, false);
        }

        internal static void AddBuiltInBehaviors(ContractDescription contract)
        {
            AddBehaviors(contract, true);
        }

        private static void AddBehaviors(ContractDescription contract, bool builtInOperationBehavior)
        {
            Reflector reflector = new Reflector(contract.Namespace, contract.ContractType);

            foreach (OperationDescription operation in contract.Operations)
            {
                Reflector.OperationReflector operationReflector = reflector.ReflectOperation(operation);
                if (operationReflector != null)
                {
                    bool isInherited = operation.DeclaringContract != contract;
                    if (!isInherited)
                    {
                        operation.Behaviors.Add(new XmlSerializerOperationBehavior(operationReflector, builtInOperationBehavior));
                        //operation.Behaviors.Add(new XmlSerializerOperationGenerator(new XmlSerializerImportOptions()));
                    }
                }
            }
        }

        internal XmlSerializerOperationFormatter CreateFormatter()
        {
            return new XmlSerializerOperationFormatter(OperationReflector.Operation, OperationReflector.Attribute, OperationReflector.Request, OperationReflector.Reply);
        }

        private XmlSerializerFaultFormatter CreateFaultFormatter(SynchronizedCollection<FaultContractInfo> faultContractInfos)
        {
            return new XmlSerializerFaultFormatter(faultContractInfos, OperationReflector.XmlSerializerFaultContractInfos);
        }

        void IOperationBehavior.Validate(OperationDescription description)
        {
        }

        void IOperationBehavior.AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
        }

        void IOperationBehavior.ApplyDispatchBehavior(OperationDescription description, DispatchOperation dispatch)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }

            if (dispatch == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dispatch));
            }

            if (dispatch.Formatter == null)
            {
                dispatch.Formatter = (IDispatchMessageFormatter)CreateFormatter();
                dispatch.DeserializeRequest = OperationReflector.RequestRequiresSerialization;
                dispatch.SerializeReply = OperationReflector.ReplyRequiresSerialization;
            }

            if (OperationReflector.Attribute.SupportFaults)
            {
                if (!dispatch.IsFaultFormatterSetExplicit)
                {
                    dispatch.FaultFormatter = (IDispatchFaultFormatter)CreateFaultFormatter(dispatch.FaultContractInfos);
                }
                else
                {
                    if (dispatch.FaultFormatter is IDispatchFaultFormatterWrapper wrapper)
                    {
                        wrapper.InnerFaultFormatter = (IDispatchFaultFormatter)CreateFaultFormatter(dispatch.FaultContractInfos);
                    }
                }
            }
        }

        void IOperationBehavior.ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }

            if (proxy == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(proxy));
            }

            if (proxy.Formatter == null)
            {
                proxy.Formatter = (IClientMessageFormatter)CreateFormatter();
                proxy.SerializeRequest = OperationReflector.RequestRequiresSerialization;
                proxy.DeserializeReply = OperationReflector.ReplyRequiresSerialization;
            }

            if (OperationReflector.Attribute.SupportFaults && !proxy.IsFaultFormatterSetExplicit)
            {
                proxy.FaultFormatter = (IClientFaultFormatter)CreateFaultFormatter(proxy.FaultContractInfos);
            }
        }

        void IWsdlExportExtension.ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext endpointContext)
        {
            if (exporter == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exporter));
            if (endpointContext == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpointContext));

            MessageContractExporter.ExportMessageBinding(exporter, endpointContext, typeof(XmlSerializerMessageContractExporter), OperationReflector.Operation);
        }

        void IWsdlExportExtension.ExportContract(WsdlExporter exporter, WsdlContractConversionContext contractContext)
        {
            if (exporter == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(exporter));
            if (contractContext == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contractContext));

            new XmlSerializerMessageContractExporter(exporter, contractContext, OperationReflector.Operation, this).ExportMessageContract();
        }

        public Collection<XmlMapping> GetXmlMappings()
        {
            Collection<XmlMapping> mappings = new Collection<XmlMapping>();
            if (OperationReflector.Request != null && OperationReflector.Request.HeadersMapping != null)
            {
                mappings.Add(OperationReflector.Request.HeadersMapping);
            }

            if (OperationReflector.Request != null && OperationReflector.Request.BodyMapping != null)
            {
                mappings.Add(OperationReflector.Request.BodyMapping);
            }

            if (OperationReflector.Reply != null && OperationReflector.Reply.HeadersMapping != null)
            {
                mappings.Add(OperationReflector.Reply.HeadersMapping);
            }

            if (OperationReflector.Reply != null && OperationReflector.Reply.BodyMapping != null)
            {
                mappings.Add(OperationReflector.Reply.BodyMapping);
            }

            return mappings;
        }

        // helper for reflecting operations
        internal class Reflector
        {
            private readonly XmlSerializerImporter _importer;
            private readonly SerializerGenerationContext _generation;
            private readonly Collection<OperationReflector> _operationReflectors = new Collection<OperationReflector>();
            private readonly object _thisLock = new object();

            internal Reflector(string defaultNs, Type type)
            {
                _importer = new XmlSerializerImporter(defaultNs);
                _generation = new SerializerGenerationContext(type);
            }

            internal void EnsureMessageInfos()
            {
                lock (_thisLock)
                {
                    foreach (OperationReflector operationReflector in _operationReflectors)
                    {
                        operationReflector.EnsureMessageInfos();
                    }
                }
            }

            private static XmlSerializerFormatAttribute FindAttribute(OperationDescription operation)
            {
                Type contractType = operation.DeclaringContract?.ContractType;
                XmlSerializerFormatAttribute contractFormatAttribute = contractType != null ? TypeLoader.GetFormattingAttribute(contractType.GetTypeInfo(), null) as XmlSerializerFormatAttribute : null;
                return TypeLoader.GetFormattingAttribute(operation.OperationMethod, contractFormatAttribute) as XmlSerializerFormatAttribute;
            }

            // auto-reflects the operation, returning null if no attribute was found or inherited
            internal OperationReflector ReflectOperation(OperationDescription operation)
            {
                XmlSerializerFormatAttribute attr = FindAttribute(operation);
                if (attr == null)
                {
                    return null;
                }

                return ReflectOperation(operation, attr);
            }

            // overrides the auto-reflection with an attribute
            internal OperationReflector ReflectOperation(OperationDescription operation, XmlSerializerFormatAttribute attrOverride)
            {
                OperationReflector operationReflector = new OperationReflector(this, operation, attrOverride, true/*reflectOnDemand*/);
                _operationReflectors.Add(operationReflector);

                return operationReflector;
            }

            internal class OperationReflector
            {
                private readonly Reflector _parent;

                internal readonly OperationDescription Operation;
                internal readonly XmlSerializerFormatAttribute Attribute;

                internal readonly bool IsEncoded;
                internal readonly bool IsRpc;
                internal readonly bool IsOneWay;
                internal readonly bool RequestRequiresSerialization;
                internal readonly bool ReplyRequiresSerialization;
                private readonly string _keyBase;
                private MessageInfo _request;
                private MessageInfo _reply;
                private SynchronizedCollection<XmlSerializerFaultContractInfo> _xmlSerializerFaultContractInfos;

                internal OperationReflector(Reflector parent, OperationDescription operation, XmlSerializerFormatAttribute attr, bool reflectOnDemand)
                {
                    Fx.Assert(parent != null, "");
                    Fx.Assert(operation != null, "");
                    Fx.Assert(attr != null, "");

                    OperationFormatter.Validate(operation, attr.Style == OperationFormatStyle.Rpc, attr.IsEncoded);

                    _parent = parent;

                    Operation = operation;
                    Attribute = attr;

                    IsEncoded = attr.IsEncoded;
                    IsRpc = (attr.Style == OperationFormatStyle.Rpc);
                    IsOneWay = operation.Messages.Count == 1;

                    RequestRequiresSerialization = !operation.Messages[0].IsUntypedMessage;
                    ReplyRequiresSerialization = !IsOneWay && !operation.Messages[1].IsUntypedMessage;

                    MethodInfo methodInfo = operation.OperationMethod;
                    if (methodInfo == null)
                    {
                        // keyBase needs to be unique within the scope of the parent reflector
                        _keyBase = string.Empty;
                        if (operation.DeclaringContract != null)
                        {
                            _keyBase = operation.DeclaringContract.Name + "," + operation.DeclaringContract.Namespace + ":";
                        }
                        _keyBase = _keyBase + operation.Name;
                    }
                    else
                    {
                        _keyBase = methodInfo.DeclaringType.FullName + ":" + methodInfo.ToString();
                    }

                    foreach (MessageDescription message in operation.Messages)
                    {
                        foreach (MessageHeaderDescription header in message.Headers)
                        {
                            SetUnknownHeaderInDescription(header);
                        }
                    }

                    if (!reflectOnDemand)
                    {
                        EnsureMessageInfos();
                    }
                }

                private void SetUnknownHeaderInDescription(MessageHeaderDescription header)
                {
                    if (IsEncoded) //XmlAnyElementAttribute does not apply
                    {
                        return;
                    }

                    if (header.AdditionalAttributesProvider != null)
                    {
                        object[] attributes = header
                            .AdditionalAttributesProvider
                            .GetCustomAttributes(false)
                            .ToArray();

                        bool isUnknown = false;
                        bool xmlIgnore = false;

                        foreach (object attribute in attributes)
                        {
                            if (attribute is XmlAnyElementAttribute elementAttribute)
                            {
                                if (string.IsNullOrEmpty(elementAttribute.Name))
                                {
                                    isUnknown = true;
                                }
                            }

                            // In the original full framework code using XmlAttributes, the XmlAnyElements collection is cleared
                            // if the XmlIgnore attribute is present.
                            if (attribute is XmlIgnoreAttribute)
                            {
                                xmlIgnore = true;
                                break; // XmlIgnore overrides all so no need to continue if found.
                            }
                        }

                        header.IsUnknownHeaderCollection = isUnknown && !xmlIgnore;
                    }
                }

                private string ContractName
                {
                    get { return Operation.DeclaringContract.Name; }
                }

                private string ContractNamespace
                {
                    get { return Operation.DeclaringContract.Namespace; }
                }

                internal MessageInfo Request
                {
                    get
                    {
                        _parent.EnsureMessageInfos();
                        return _request;
                    }
                }

                internal MessageInfo Reply
                {
                    get
                    {
                        _parent.EnsureMessageInfos();
                        return _reply;
                    }
                }

                internal SynchronizedCollection<XmlSerializerFaultContractInfo> XmlSerializerFaultContractInfos
                {
                    get
                    {
                        _parent.EnsureMessageInfos();
                        return _xmlSerializerFaultContractInfos;
                    }
                }

                internal void EnsureMessageInfos()
                {
                    if (_request == null)
                    {
                        foreach (Type knownType in Operation.KnownTypes)
                        {
                            if (knownType == null)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxKnownTypeNull, Operation.Name)));
                            }

                            _parent._importer.IncludeType(knownType, IsEncoded);
                        }
                        _request = CreateMessageInfo(Operation.Messages[0], ":Request");
                        if (_request != null && IsRpc && Operation.IsValidateRpcWrapperName && _request.BodyMapping.XsdElementName != Operation.Name)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxRpcMessageBodyPartNameInvalid, Operation.Name, Operation.Messages[0].MessageName, _request.BodyMapping.XsdElementName, Operation.Name)));
                        }

                        if (!IsOneWay)
                        {
                            _reply = CreateMessageInfo(Operation.Messages[1], ":Response");
                            XmlName responseName = TypeLoader.GetBodyWrapperResponseName(Operation.Name);
                            if (_reply != null && IsRpc && Operation.IsValidateRpcWrapperName && _reply.BodyMapping.XsdElementName != responseName.EncodedName)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxRpcMessageBodyPartNameInvalid, Operation.Name, Operation.Messages[1].MessageName, _reply.BodyMapping.XsdElementName, responseName.EncodedName)));
                            }
                        }
                        if (Attribute.SupportFaults)
                        {
                            GenerateXmlSerializerFaultContractInfos();
                        }
                    }
                }

                private void GenerateXmlSerializerFaultContractInfos()
                {
                    SynchronizedCollection<XmlSerializerFaultContractInfo> faultInfos = new SynchronizedCollection<XmlSerializerFaultContractInfo>();
                    for (int i = 0; i < Operation.Faults.Count; i++)
                    {
                        FaultDescription fault = Operation.Faults[i];
                        FaultContractInfo faultContractInfo = new FaultContractInfo(fault.Action, fault.DetailType, fault.ElementName, fault.Namespace, Operation.KnownTypes);

                        XmlMembersMapping xmlMembersMapping = ImportFaultElement(fault, out XmlQualifiedName elementName);

                        SerializerStub serializerStub = _parent._generation.AddSerializer(xmlMembersMapping);
                        faultInfos.Add(new XmlSerializerFaultContractInfo(faultContractInfo, serializerStub, elementName));
                    }
                    _xmlSerializerFaultContractInfos = faultInfos;
                }

                private MessageInfo CreateMessageInfo(MessageDescription message, string key)
                {
                    if (message.IsUntypedMessage)
                    {
                        return null;
                    }

                    MessageInfo info = new MessageInfo();
                    if (message.IsTypedMessage)
                    {
                        key = message.MessageType.FullName + ":" + IsEncoded + ":" + IsRpc;
                    }

                    XmlMembersMapping headersMapping = LoadHeadersMapping(message, key + ":Headers");
                    info.SetHeaders(_parent._generation.AddSerializer(headersMapping));
                    info.SetBody(_parent._generation.AddSerializer(LoadBodyMapping(message, key, out MessagePartDescriptionCollection rpcEncodedTypedMessageBodyParts)), rpcEncodedTypedMessageBodyParts);
                    CreateHeaderDescriptionTable(message, info, headersMapping);
                    return info;
                }

                private void CreateHeaderDescriptionTable(MessageDescription message, MessageInfo info, XmlMembersMapping headersMapping)
                {
                    int headerNameIndex = 0;
                    OperationFormatter.MessageHeaderDescriptionTable headerDescriptionTable = new OperationFormatter.MessageHeaderDescriptionTable();
                    info.SetHeaderDescriptionTable(headerDescriptionTable);
                    foreach (MessageHeaderDescription header in message.Headers)
                    {
                        if (header.IsUnknownHeaderCollection)
                        {
                            info.SetUnknownHeaderDescription(header);
                        }
                        else if (headersMapping != null)
                        {
                            XmlMemberMapping memberMapping = headersMapping[headerNameIndex++];
                            string headerName, headerNs;
                            if (IsEncoded)
                            {
                                headerName = memberMapping.TypeName;
                                headerNs = memberMapping.TypeNamespace;
                            }
                            else
                            {
                                headerName = memberMapping.XsdElementName;
                                headerNs = memberMapping.Namespace;
                            }
                            if (headerName != header.Name)
                            {
                                if (message.MessageType != null)
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxHeaderNameMismatchInMessageContract, message.MessageType, header.MemberInfo.Name, header.Name, headerName)));
                                }
                                else
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxHeaderNameMismatchInOperation, Operation.Name, Operation.DeclaringContract.Name, Operation.DeclaringContract.Namespace, header.Name, headerName)));
                                }
                            }
                            if (headerNs != header.Namespace)
                            {
                                if (message.MessageType != null)
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxHeaderNamespaceMismatchInMessageContract, message.MessageType, header.MemberInfo.Name, header.Namespace, headerNs)));
                                }
                                else
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxHeaderNamespaceMismatchInOperation, Operation.Name, Operation.DeclaringContract.Name, Operation.DeclaringContract.Namespace, header.Namespace, headerNs)));
                                }
                            }

                            headerDescriptionTable.Add(headerName, headerNs, header);
                        }
                    }
                }

                private XmlMembersMapping LoadBodyMapping(MessageDescription message, string mappingKey, out MessagePartDescriptionCollection rpcEncodedTypedMessageBodyParts)
                {
                    MessagePartDescription returnPart;
                    string wrapperName, wrapperNs;
                    MessagePartDescriptionCollection bodyParts;
                    if (IsEncoded && message.IsTypedMessage && message.Body.WrapperName == null)
                    {
                        MessagePartDescription wrapperPart = GetWrapperPart(message);
                        returnPart = null;
                        rpcEncodedTypedMessageBodyParts = bodyParts = GetWrappedParts(wrapperPart);
                        wrapperName = wrapperPart.Name;
                        wrapperNs = wrapperPart.Namespace;
                    }
                    else
                    {
                        rpcEncodedTypedMessageBodyParts = null;
                        returnPart = OperationFormatter.IsValidReturnValue(message.Body.ReturnValue) ? message.Body.ReturnValue : null;
                        bodyParts = message.Body.Parts;
                        wrapperName = message.Body.WrapperName;
                        wrapperNs = message.Body.WrapperNamespace;
                    }
                    bool isWrapped = (wrapperName != null);
                    bool hasReturnValue = returnPart != null;
                    int paramCount = bodyParts.Count + (hasReturnValue ? 1 : 0);
                    if (paramCount == 0 && !isWrapped) // no need to create serializer
                    {
                        return null;
                    }

                    XmlReflectionMember[] members = new XmlReflectionMember[paramCount];
                    int paramIndex = 0;
                    if (hasReturnValue)
                    {
                        members[paramIndex++] = XmlSerializerHelper.GetXmlReflectionMember(returnPart, IsRpc, IsEncoded, isWrapped);
                    }

                    for (int i = 0; i < bodyParts.Count; i++)
                    {
                        members[paramIndex++] = XmlSerializerHelper.GetXmlReflectionMember(bodyParts[i], IsRpc, IsEncoded, isWrapped);
                    }

                    if (!isWrapped)
                    {
                        wrapperNs = ContractNamespace;
                    }

                    return ImportMembersMapping(wrapperName, wrapperNs, members, isWrapped, IsRpc, mappingKey);
                }

                private MessagePartDescription GetWrapperPart(MessageDescription message)
                {
                    if (message.Body.Parts.Count != 1)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxRpcMessageMustHaveASingleBody, Operation.Name, message.MessageName)));
                    }

                    MessagePartDescription bodyPart = message.Body.Parts[0];
                    Type bodyObjectType = bodyPart.Type;
                    if (bodyObjectType.GetTypeInfo().BaseType != null && bodyObjectType.GetTypeInfo().BaseType != typeof(object))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxBodyObjectTypeCannotBeInherited, bodyObjectType.FullName)));
                    }

                    if (typeof(IEnumerable).IsAssignableFrom(bodyObjectType))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxBodyObjectTypeCannotBeInterface, bodyObjectType.FullName, typeof(IEnumerable).FullName)));
                    }

                    if (typeof(IXmlSerializable).IsAssignableFrom(bodyObjectType))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxBodyObjectTypeCannotBeInterface, bodyObjectType.FullName, typeof(IXmlSerializable).FullName)));
                    }

                    return bodyPart;
                }

                private MessagePartDescriptionCollection GetWrappedParts(MessagePartDescription bodyPart)
                {
                    Type bodyObjectType = bodyPart.Type;
                    MessagePartDescriptionCollection partList = new MessagePartDescriptionCollection();
                    foreach (MemberInfo member in bodyObjectType.GetMembers(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if ((member as PropertyInfo) == null && (member as FieldInfo) == null)
                        {
                            continue;
                        }

                        if (member.IsDefined(typeof(SoapIgnoreAttribute), inherit: false))
                        {
                            continue;
                        }

                        XmlName xmlName = new XmlName(member.Name);
                        MessagePartDescription part = new MessagePartDescription(xmlName.EncodedName, string.Empty);
                        part.AdditionalAttributesProvider = part.MemberInfo = member;
                        part.Index = part.SerializationPosition = partList.Count;
                        part.Type = (member is PropertyInfo) ? ((PropertyInfo)member).PropertyType : ((FieldInfo)member).FieldType;
                        partList.Add(part);
                    }
                    return partList;
                }

                private XmlMembersMapping LoadHeadersMapping(MessageDescription message, string mappingKey)
                {
                    int headerCount = message.Headers.Count;

                    if (headerCount == 0)
                    {
                        return null;
                    }

                    if (IsEncoded)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxHeadersAreNotSupportedInEncoded, message.MessageName)));
                    }

                    int unknownHeaderCount = 0, headerIndex = 0;
                    XmlReflectionMember[] members = new XmlReflectionMember[headerCount];
                    for (int i = 0; i < headerCount; i++)
                    {
                        MessageHeaderDescription header = message.Headers[i];
                        if (!header.IsUnknownHeaderCollection)
                        {
                            members[headerIndex++] = XmlSerializerHelper.GetXmlReflectionMember(header, false/*isRpc*/, IsEncoded, false/*isWrapped*/);
                        }
                        else
                        {
                            unknownHeaderCount++;
                        }
                    }

                    if (unknownHeaderCount == headerCount)
                    {
                        return null;
                    }

                    if (unknownHeaderCount > 0)
                    {
                        XmlReflectionMember[] newMembers = new XmlReflectionMember[headerCount - unknownHeaderCount];
                        Array.Copy(members, newMembers, newMembers.Length);
                        members = newMembers;
                    }

                    return ImportMembersMapping(ContractName, ContractNamespace, members, false /*isWrapped*/, false /*isRpc*/, mappingKey);
                }

                internal XmlMembersMapping ImportMembersMapping(string elementName, string ns, XmlReflectionMember[] members, bool hasWrapperElement, bool rpc, string mappingKey)
                {
                    string key = mappingKey.StartsWith(":", StringComparison.Ordinal) ? _keyBase + mappingKey : mappingKey;
                    return _parent._importer.ImportMembersMapping(new XmlName(elementName, true /*isEncoded*/), ns, members, hasWrapperElement, rpc, IsEncoded, key);
                }

                internal XmlMembersMapping ImportFaultElement(FaultDescription fault, out XmlQualifiedName elementName)
                {
                    // the number of reflection members is always 1 because there is only one fault detail type
                    XmlReflectionMember[] members = new XmlReflectionMember[1];

                    XmlName faultElementName = fault.ElementName;
                    string faultNamespace = fault.Namespace;
                    if (faultElementName == null)
                    {
                        XmlTypeMapping mapping = _parent._importer.ImportTypeMapping(fault.DetailType, IsEncoded);
                        faultElementName = new XmlName(mapping.ElementName, IsEncoded);
                        faultNamespace = mapping.Namespace;
                        if (faultElementName == null)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxFaultTypeAnonymous, Operation.Name, fault.DetailType.FullName)));
                        }
                    }

                    elementName = new XmlQualifiedName(faultElementName.DecodedName, faultNamespace);

                    members[0] = XmlSerializerHelper.GetXmlReflectionMember(null /*memberName*/, faultElementName, faultNamespace, fault.DetailType,
                        null /*additionalAttributesProvider*/, false /*isMultiple*/, IsEncoded, false /*isWrapped*/);

                    string mappingKey = "fault:" + faultElementName.DecodedName + ":" + faultNamespace;
                    return ImportMembersMapping(faultElementName.EncodedName, faultNamespace, members, false /*hasWrapperElement*/, IsRpc, mappingKey);
                }
            }

            private class XmlSerializerImporter
            {
                private readonly string _defaultNs;
                private XmlReflectionImporter _xmlImporter;
                private SoapReflectionImporter _soapImporter;
                private Dictionary<string, XmlMembersMapping> _xmlMappings;

                internal XmlSerializerImporter(string defaultNs)
                {
                    _defaultNs = defaultNs;
                    _xmlImporter = null;
                    _soapImporter = null;
                }

                private SoapReflectionImporter SoapImporter
                {
                    get
                    {
                        if (_soapImporter == null)
                        {
                            _soapImporter = new SoapReflectionImporter(NamingHelper.CombineUriStrings(_defaultNs, "encoded"));
                        }
                        return _soapImporter;
                    }
                }

                private XmlReflectionImporter XmlImporter
                {
                    get
                    {
                        if (_xmlImporter == null)
                        {
                            _xmlImporter = new XmlReflectionImporter(_defaultNs);
                        }
                        return _xmlImporter;
                    }
                }

                private Dictionary<string, XmlMembersMapping> XmlMappings
                {
                    get
                    {
                        if (_xmlMappings == null)
                        {
                            _xmlMappings = new Dictionary<string, XmlMembersMapping>();
                        }
                        return _xmlMappings;
                    }
                }

                internal XmlMembersMapping ImportMembersMapping(XmlName elementName, string ns, XmlReflectionMember[] members, bool hasWrapperElement, bool rpc, bool isEncoded, string mappingKey)
                {
                    string mappingName = elementName.DecodedName;
                    if (XmlMappings.TryGetValue(mappingKey, out XmlMembersMapping mapping))
                    {
                        return mapping;
                    }

                    if (isEncoded)
                    {
                        mapping = SoapImporter.ImportMembersMapping(mappingName, ns, members, hasWrapperElement, rpc);
                    }
                    else
                    {
                        mapping = XmlImporter.ImportMembersMapping(mappingName, ns, members, hasWrapperElement, rpc);
                    }

                    mapping.SetKey(mappingKey);
                    XmlMappings.Add(mappingKey, mapping);
                    return mapping;
                }

                internal XmlTypeMapping ImportTypeMapping(Type type, bool isEncoded)
                {
                    if (isEncoded)
                    {
                        return SoapImporter.ImportTypeMapping(type);
                    }
                    else
                    {
                        return XmlImporter.ImportTypeMapping(type);
                    }
                }

                internal void IncludeType(Type knownType, bool isEncoded)
                {
                    if (isEncoded)
                    {
                        SoapImporter.IncludeType(knownType);
                    }
                    else
                    {
                        XmlImporter.IncludeType(knownType);
                    }
                }
            }

            internal class SerializerGenerationContext
            {
                private readonly List<XmlMembersMapping> _mappings = new List<XmlMembersMapping>();
                private XmlSerializer[] _serializers = null;
                private readonly Type _type;
                private readonly object _thisLock = new object();

                internal SerializerGenerationContext(Type type)
                {
                    _type = type;
                }

                // returns a stub to a serializer
                internal SerializerStub AddSerializer(XmlMembersMapping mapping)
                {
                    int handle = -1;
                    if (mapping != null)
                    {
                        handle = ((IList)_mappings).Add(mapping);
                    }

                    return new SerializerStub(this, mapping, handle);
                }

                internal XmlSerializer GetSerializer(int handle)
                {
                    if (handle < 0)
                    {
                        return null;
                    }

                    if (_serializers == null)
                    {
                        lock (_thisLock)
                        {
                            if (_serializers == null)
                            {
                                _serializers = GenerateSerializers();
                            }
                        }
                    }
                    return _serializers[handle];
                }

                private XmlSerializer[] GenerateSerializers()
                {
                    //this.Mappings may have duplicate mappings (for e.g. same message contract is used by more than one operation)
                    //XmlSerializer.FromMappings require unique mappings. The following code uniquifies, calls FromMappings and deuniquifies
                    List<XmlMembersMapping> uniqueMappings = new List<XmlMembersMapping>();
                    int[] uniqueIndexes = new int[_mappings.Count];
                    for (int srcIndex = 0; srcIndex < _mappings.Count; srcIndex++)
                    {
                        XmlMembersMapping mapping = _mappings[srcIndex];
                        int uniqueIndex = uniqueMappings.IndexOf(mapping);
                        if (uniqueIndex < 0)
                        {
                            uniqueMappings.Add(mapping);
                            uniqueIndex = uniqueMappings.Count - 1;
                        }
                        uniqueIndexes[srcIndex] = uniqueIndex;
                    }
                    XmlSerializer[] uniqueSerializers = CreateSerializersFromMappings(uniqueMappings.ToArray(), _type);
                    if (uniqueMappings.Count == _mappings.Count)
                    {
                        return uniqueSerializers;
                    }

                    XmlSerializer[] serializers = new XmlSerializer[_mappings.Count];
                    for (int i = 0; i < _mappings.Count; i++)
                    {
                        serializers[i] = uniqueSerializers[uniqueIndexes[i]];
                    }
                    return serializers;
                }

                private XmlSerializer[] CreateSerializersFromMappings(XmlMapping[] mappings, Type type)
                {
                    return XmlSerializer.FromMappings(mappings, type);
                }
            }

            internal struct SerializerStub
            {
                private readonly SerializerGenerationContext _context;

                internal readonly XmlMembersMapping Mapping;
                internal readonly int Handle;

                internal SerializerStub(SerializerGenerationContext context, XmlMembersMapping mapping, int handle)
                {
                    _context = context;
                    Mapping = mapping;
                    Handle = handle;
                }

                internal XmlSerializer GetSerializer()
                {
                    return _context.GetSerializer(Handle);
                }
            }

            internal class XmlSerializerFaultContractInfo
            {
                private readonly SerializerStub _serializerStub;
                private XmlSerializerObjectSerializer _serializer;

                internal XmlSerializerFaultContractInfo(FaultContractInfo faultContractInfo, SerializerStub serializerStub,
                    XmlQualifiedName faultContractElementName)
                {
                    FaultContractInfo = faultContractInfo ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(faultContractInfo));
                    _serializerStub = serializerStub;
                    FaultContractElementName = faultContractElementName ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(faultContractElementName));
                }

                internal FaultContractInfo FaultContractInfo { get; }

                internal XmlQualifiedName FaultContractElementName { get; }

                internal XmlSerializerObjectSerializer Serializer
                {
                    get
                    {
                        if (_serializer == null)
                        {
                            _serializer = new XmlSerializerObjectSerializer(FaultContractInfo.Detail, FaultContractElementName, _serializerStub.GetSerializer());
                        }

                        return _serializer;
                    }
                }
            }

            internal class MessageInfo : XmlSerializerOperationFormatter.MessageInfo
            {
                private SerializerStub _headers;
                private SerializerStub _body;
                private OperationFormatter.MessageHeaderDescriptionTable _headerDescriptionTable;
                private MessageHeaderDescription _unknownHeaderDescription;
                private MessagePartDescriptionCollection _rpcEncodedTypedMessageBodyParts;

                internal XmlMembersMapping BodyMapping
                {
                    get { return _body.Mapping; }
                }

                internal override XmlSerializer BodySerializer
                {
                    get { return _body.GetSerializer(); }
                }

                internal XmlMembersMapping HeadersMapping
                {
                    get { return _headers.Mapping; }
                }

                internal override XmlSerializer HeaderSerializer
                {
                    get { return _headers.GetSerializer(); }
                }

                internal override OperationFormatter.MessageHeaderDescriptionTable HeaderDescriptionTable
                {
                    get { return _headerDescriptionTable; }
                }

                internal override MessageHeaderDescription UnknownHeaderDescription
                {
                    get { return _unknownHeaderDescription; }
                }

                internal override MessagePartDescriptionCollection RpcEncodedTypedMessageBodyParts
                {
                    get { return _rpcEncodedTypedMessageBodyParts; }
                }

                internal void SetBody(SerializerStub body, MessagePartDescriptionCollection rpcEncodedTypedMessageBodyParts)
                {
                    _body = body;
                    _rpcEncodedTypedMessageBodyParts = rpcEncodedTypedMessageBodyParts;
                }

                internal void SetHeaders(SerializerStub headers)
                {
                    _headers = headers;
                }

                internal void SetHeaderDescriptionTable(OperationFormatter.MessageHeaderDescriptionTable headerDescriptionTable)
                {
                    _headerDescriptionTable = headerDescriptionTable;
                }

                internal void SetUnknownHeaderDescription(MessageHeaderDescription unknownHeaderDescription)
                {
                    _unknownHeaderDescription = unknownHeaderDescription;
                }
            }
        }
    }

    internal static class XmlSerializerHelper
    {
        internal static XmlReflectionMember GetXmlReflectionMember(MessagePartDescription part, bool isRpc, bool isEncoded, bool isWrapped)
        {
            string ns = isRpc ? null : part.Namespace;
            ICustomAttributeProvider additionalAttributesProvider = null;
            if (isEncoded || part.AdditionalAttributesProvider is MemberInfo)
                additionalAttributesProvider = part.AdditionalAttributesProvider;

            XmlName memberName = string.IsNullOrEmpty(part.UniquePartName) ? null : new XmlName(part.UniquePartName, true /*isEncoded*/);
            XmlName elementName = part.XmlName;
            return GetXmlReflectionMember(memberName, elementName, ns, part.Type, additionalAttributesProvider, part.Multiple, isEncoded, isWrapped);
        }

        internal static XmlReflectionMember GetXmlReflectionMember(XmlName memberName, XmlName elementName, string ns, Type type, ICustomAttributeProvider additionalAttributesProvider, bool isMultiple, bool isEncoded, bool isWrapped)
        {
            if (isEncoded && isMultiple)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxMultiplePartsNotAllowedInEncoded, elementName.DecodedName, ns)));
            }

            XmlReflectionMember member = new XmlReflectionMember
            {
                MemberName = (memberName ?? elementName).DecodedName,
                MemberType = type
            };
            if (member.MemberType.IsByRef)
            {
                member.MemberType = member.MemberType.GetElementType();
            }

            if (isMultiple)
            {
                member.MemberType = member.MemberType.MakeArrayType();
            }

            if (additionalAttributesProvider != null)
            {
                if (isEncoded)
                {
                    member.SoapAttributes = new SoapAttributes(additionalAttributesProvider);
                }
                else
                {
                    member.XmlAttributes = new XmlAttributes(additionalAttributesProvider);
                }
            }

            if (isEncoded)
            {
                if (member.SoapAttributes is null)
                {
                    member.SoapAttributes = new SoapAttributes();
                }
                else
                {
                    Type invalidAttributeType = null;
                    if (member.SoapAttributes.SoapAttribute != null)
                    {
                        invalidAttributeType = typeof(SoapAttributeAttribute);
                    }
                    else if (member.SoapAttributes.SoapIgnore)
                    {
                        invalidAttributeType = typeof(SoapIgnoreAttribute);
                    }
                    else if (member.SoapAttributes.SoapType != null)
                    {
                        invalidAttributeType = typeof(SoapTypeAttribute);
                    }

                    if (invalidAttributeType != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInvalidSoapAttribute, invalidAttributeType, elementName.DecodedName)));
                    }
                }

                if (member.SoapAttributes.SoapElement == null)
                {
                    member.SoapAttributes.SoapElement = new SoapElementAttribute(elementName.DecodedName);
                }
            }
            else
            {
                if (member.XmlAttributes is null)
                {
                    member.XmlAttributes = new XmlAttributes();
                }
                else
                {
                    Type invalidAttributeType = null;
                    if (member.XmlAttributes.XmlAttribute != null)
                    {
                        invalidAttributeType = typeof(XmlAttributeAttribute);
                    }
                    else if (member.XmlAttributes.XmlAnyAttribute != null && !isWrapped)
                    {
                        invalidAttributeType = typeof(XmlAnyAttributeAttribute);
                    }
                    else if (member.XmlAttributes.XmlChoiceIdentifier != null)
                    {
                        invalidAttributeType = typeof(XmlChoiceIdentifierAttribute);
                    }
                    else if (member.XmlAttributes.XmlIgnore)
                    {
                        invalidAttributeType = typeof(XmlIgnoreAttribute);
                    }
                    else if (member.XmlAttributes.Xmlns)
                    {
                        invalidAttributeType = typeof(XmlNamespaceDeclarationsAttribute);
                    }
                    else if (member.XmlAttributes.XmlText != null)
                    {
                        invalidAttributeType = typeof(XmlTextAttribute);
                    }
                    else if (member.XmlAttributes.XmlEnum != null)
                    {
                        invalidAttributeType = typeof(XmlEnumAttribute);
                    }

                    if (invalidAttributeType != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(isWrapped ? SR.SFxInvalidXmlAttributeInWrapped : SR.SFxInvalidXmlAttributeInBare, invalidAttributeType, elementName.DecodedName)));
                    }

                    if (member.XmlAttributes.XmlArray != null && isMultiple)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxXmlArrayNotAllowedForMultiple, elementName.DecodedName, ns)));
                    }
                }

                bool isArray = member.MemberType.IsArray;
                if ((isArray && !isMultiple && member.MemberType != typeof(byte[])) ||
                   (!isArray && typeof(IEnumerable).IsAssignableFrom(member.MemberType) && member.MemberType != typeof(string) && !typeof(XmlNode).IsAssignableFrom(member.MemberType) && !typeof(IXmlSerializable).IsAssignableFrom(member.MemberType)))
                {
                    if (member.XmlAttributes.XmlArray != null)
                    {
                        if (member.XmlAttributes.XmlArray.ElementName == string.Empty)
                        {
                            member.XmlAttributes.XmlArray.ElementName = elementName.DecodedName;
                        }

                        if (member.XmlAttributes.XmlArray.Namespace == null)
                        {
                            member.XmlAttributes.XmlArray.Namespace = ns;
                        }
                    }
                    else if (HasNoXmlParameterAttributes(member.XmlAttributes))
                    {
                        member.XmlAttributes.XmlArray = new XmlArrayAttribute
                        {
                            ElementName = elementName.DecodedName,
                            Namespace = ns
                        };
                    }
                }
                else
                {
                    if (member.XmlAttributes.XmlElements == null || member.XmlAttributes.XmlElements.Count == 0)
                    {
                        if (HasNoXmlParameterAttributes(member.XmlAttributes))
                        {
                            XmlElementAttribute elementAttribute = new XmlElementAttribute
                            {
                                ElementName = elementName.DecodedName,
                                Namespace = ns
                            };
                            member.XmlAttributes.XmlElements.Add(elementAttribute);
                        }
                    }
                    else
                    {
                        foreach (XmlElementAttribute elementAttribute in member.XmlAttributes.XmlElements)
                        {
                            if (elementAttribute.ElementName == string.Empty)
                            {
                                elementAttribute.ElementName = elementName.DecodedName;
                            }

                            if (elementAttribute.Namespace == null)
                            {
                                elementAttribute.Namespace = ns;
                            }
                        }
                    }
                }
            }

            return member;
        }

        private static bool HasNoXmlParameterAttributes(XmlAttributes xmlAttributes)
        {
            return xmlAttributes.XmlAnyAttribute == null &&
                (xmlAttributes.XmlAnyElements == null || xmlAttributes.XmlAnyElements.Count == 0) &&
                xmlAttributes.XmlArray == null &&
                xmlAttributes.XmlAttribute == null &&
                !xmlAttributes.XmlIgnore &&
                xmlAttributes.XmlText == null &&
                xmlAttributes.XmlChoiceIdentifier == null &&
                (xmlAttributes.XmlElements == null || xmlAttributes.XmlElements.Count == 0) &&
                !xmlAttributes.Xmlns;
        }
    }
}
