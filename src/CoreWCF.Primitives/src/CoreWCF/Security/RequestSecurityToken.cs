// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Authentication.ExtendedProtection;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class RequestSecurityToken : BodyWriter
    {
        private string context;
        private string tokenType;
        private string requestType;
        private SecurityToken entropyToken;
        private BinaryNegotiation negotiationData;
        private readonly XmlElement rstXml;
        private IList<XmlElement> requestProperties;
        private byte[] cachedWriteBuffer;
        private int cachedWriteBufferLength;
        private int keySize;
        private SecurityKeyIdentifierClause renewTarget;
        private SecurityKeyIdentifierClause closeTarget;
        private OnGetBinaryNegotiationCallback onGetBinaryNegotiation;
        private SecurityStandardsManager standardsManager;
        private bool isReadOnly;
        private object appliesTo;
        private DataContractSerializer appliesToSerializer;
        private Type appliesToType;

        public RequestSecurityToken()
            : this(SecurityStandardsManager.DefaultInstance)
        {
        }

        public RequestSecurityToken(MessageSecurityVersion messageSecurityVersion, SecurityTokenSerializer securityTokenSerializer)
            : this(SecurityUtils.CreateSecurityStandardsManager(messageSecurityVersion, securityTokenSerializer))
        {
        }

        public RequestSecurityToken(MessageSecurityVersion messageSecurityVersion,
                                    SecurityTokenSerializer securityTokenSerializer,
                                    XmlElement requestSecurityTokenXml,
                                    string context,
                                    string tokenType,
                                    string requestType,
                                    int keySize,
                                    SecurityKeyIdentifierClause renewTarget,
                                    SecurityKeyIdentifierClause closeTarget)
            : this(SecurityUtils.CreateSecurityStandardsManager(messageSecurityVersion, securityTokenSerializer),
                   requestSecurityTokenXml,
                   context,
                   tokenType,
                   requestType,
                   keySize,
                   renewTarget,
                   closeTarget)
        {
        }

        public RequestSecurityToken(XmlElement requestSecurityTokenXml,
                                    string context,
                                    string tokenType,
                                    string requestType,
                                    int keySize,
                                    SecurityKeyIdentifierClause renewTarget,
                                    SecurityKeyIdentifierClause closeTarget)
            : this(SecurityStandardsManager.DefaultInstance,
                   requestSecurityTokenXml,
                   context,
                   tokenType,
                   requestType,
                   keySize,
                   renewTarget,
                   closeTarget)
        {
        }

        internal RequestSecurityToken(SecurityStandardsManager standardsManager,
                                      XmlElement rstXml,
                                      string context,
                                      string tokenType,
                                      string requestType,
                                      int keySize,
                                      SecurityKeyIdentifierClause renewTarget,
                                      SecurityKeyIdentifierClause closeTarget)
            : base(true)
        {
            if (standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("standardsManager"));
            }
            this.standardsManager = standardsManager;
            if (rstXml == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("rstXml");
            }

            this.rstXml = rstXml;
            this.context = context;
            this.tokenType = tokenType;
            this.keySize = keySize;
            this.requestType = requestType;
            this.renewTarget = renewTarget;
            this.closeTarget = closeTarget;
            IsReceiver = true;
            isReadOnly = true;
        }

        internal RequestSecurityToken(SecurityStandardsManager standardsManager)
            : this(standardsManager, true)
        {
            // no op
        }

        internal RequestSecurityToken(SecurityStandardsManager standardsManager, bool isBuffered)
            : base(isBuffered)
        {
            if (standardsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("standardsManager"));
            }
            this.standardsManager = standardsManager;
            requestType = this.standardsManager.TrustDriver.RequestTypeIssue;
            requestProperties = null;
            IsReceiver = false;
            isReadOnly = false;
        }

        public ChannelBinding GetChannelBinding()
        {
            if (Message == null)
            {
                return null;
            }

            ChannelBindingMessageProperty.TryGet(Message, out ChannelBindingMessageProperty channelBindingMessageProperty);
            ChannelBinding channelBinding = null;

            if (channelBindingMessageProperty != null)
            {
                channelBinding = channelBindingMessageProperty.ChannelBinding;
            }

            return channelBinding;
        }

        /// <summary>
        /// Will hold a reference to the outbound message from which we will fish the ChannelBinding out of.
        /// </summary>
        public Message Message { get; set; }

        public string Context
        {
            get
            {
                return context;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                context = value;
            }
        }

        public string TokenType
        {
            get
            {
                return tokenType;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                tokenType = value;
            }
        }

        public int KeySize
        {
            get
            {
                return keySize;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", SR.ValueMustBeNonNegative));
                }

                keySize = value;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return isReadOnly;
            }
        }

        public delegate void OnGetBinaryNegotiationCallback(ChannelBinding channelBinding);
        public OnGetBinaryNegotiationCallback OnGetBinaryNegotiation
        {
            get
            {
                return onGetBinaryNegotiation;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }
                onGetBinaryNegotiation = value;
            }
        }

        public IEnumerable<XmlElement> RequestProperties
        {
            get
            {
                if (IsReceiver)
                {
                    // PreSharp Bug: Property get methods should not throw exceptions.
#pragma warning suppress 56503
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRST, "RequestProperties")));
                }
                return requestProperties;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                if (value != null)
                {
                    int index = 0;
                    Collection<XmlElement> coll = new Collection<XmlElement>();
                    foreach (XmlElement property in value)
                    {
                        if (property == null)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(String.Format(CultureInfo.InvariantCulture, "value[{0}]", index)));
                        }

                        coll.Add(property);
                        ++index;
                    }
                    requestProperties = coll;
                }
                else
                {
                    requestProperties = null;
                }
            }
        }

        public string RequestType
        {
            get
            {
                return requestType;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("value");
                }

                requestType = value;
            }
        }

        public SecurityKeyIdentifierClause RenewTarget
        {
            get
            {
                return renewTarget;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                renewTarget = value;
            }
        }

        public SecurityKeyIdentifierClause CloseTarget
        {
            get
            {
                return closeTarget;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                closeTarget = value;
            }
        }

        public XmlElement RequestSecurityTokenXml
        {
            get
            {
                if (!IsReceiver)
                {
                    // PreSharp Bug: Property get methods should not throw exceptions.
#pragma warning suppress 56503
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemAvailableInDeserializedRSTOnly, "RequestSecurityTokenXml")));
                }
                return rstXml;
            }
        }

        internal SecurityStandardsManager StandardsManager
        {
            get
            {
                return standardsManager;
            }
            set
            {
                if (IsReadOnly)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
                }

                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("value"));
                }
                standardsManager = value;
            }
        }

        internal bool IsReceiver { get; }

        internal object AppliesTo
        {
            get
            {
                if (IsReceiver)
                {
                    // PreSharp Bug: Property get methods should not throw exceptions.
#pragma warning suppress 56503
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRST, "AppliesTo")));
                }
                return appliesTo;
            }
        }

        internal DataContractSerializer AppliesToSerializer
        {
            get
            {
                if (IsReceiver)
                {
                    // PreSharp Bug: Property get methods should not throw exceptions.
#pragma warning suppress 56503
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRST, "AppliesToSerializer")));
                }
                return appliesToSerializer;
            }
        }

        internal Type AppliesToType
        {
            get
            {
                if (IsReceiver)
                {
                    // PreSharp Bug: Property get methods should not throw exceptions.
#pragma warning suppress 56503
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemNotAvailableInDeserializedRST, "AppliesToType")));
                }
                return appliesToType;
            }
        }

        protected Object ThisLock { get; } = new Object();

        internal void SetBinaryNegotiation(BinaryNegotiation negotiation)
        {
            if (negotiation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("negotiation");
            }

            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
            }

            negotiationData = negotiation;
        }

        internal BinaryNegotiation GetBinaryNegotiation()
        {
            if (IsReceiver)
            {
                return standardsManager.TrustDriver.GetBinaryNegotiation(this);
            }
            else if (negotiationData == null && onGetBinaryNegotiation != null)
            {
                onGetBinaryNegotiation(GetChannelBinding());
            }
            return negotiationData;
        }

        public SecurityToken GetRequestorEntropy()
        {
            return GetRequestorEntropy(null);
        }

        internal SecurityToken GetRequestorEntropy(SecurityTokenResolver resolver)
        {
            if (IsReceiver)
            {
                return standardsManager.TrustDriver.GetEntropy(this, resolver);
            }
            else
            {
                return entropyToken;
            }
        }

        //public void SetRequestorEntropy(byte[] entropy)
        //{
        //    if (this.IsReadOnly)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
        //    this.entropyToken = (entropy != null) ? new NonceToken(entropy) : null;
        //}

        internal void SetRequestorEntropy(WrappedKeySecurityToken entropyToken)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
            }

            this.entropyToken = entropyToken;
        }

        public void SetAppliesTo<T>(T appliesTo, DataContractSerializer serializer)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
            }

            if (appliesTo != null && serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("serializer");
            }
            this.appliesTo = appliesTo;
            appliesToSerializer = serializer;
            appliesToType = typeof(T);
        }

        public void GetAppliesToQName(out string localName, out string namespaceUri)
        {
            if (!IsReceiver)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ItemAvailableInDeserializedRSTOnly, "MatchesAppliesTo")));
            }

            standardsManager.TrustDriver.GetAppliesToQName(this, out localName, out namespaceUri);
        }

        public T GetAppliesTo<T>()
        {
            return GetAppliesTo<T>(DataContractSerializerDefaults.CreateSerializer(typeof(T), DataContractSerializerDefaults.MaxItemsInObjectGraph));
        }

        public T GetAppliesTo<T>(XmlObjectSerializer serializer)
        {
            if (IsReceiver)
            {
                if (serializer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("serializer");
                }
                return standardsManager.TrustDriver.GetAppliesTo<T>(this, serializer);
            }
            else
            {
                return (T)appliesTo;
            }
        }

        private void OnWriteTo(XmlWriter writer)
        {
            if (IsReceiver)
            {
                rstXml.WriteTo(writer);
            }
            else
            {
                standardsManager.TrustDriver.WriteRequestSecurityToken(this, writer);
            }
        }

        public void WriteTo(XmlWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("writer");
            }

            if (IsReadOnly)
            {
                // cache the serialized bytes to ensure repeatability
                if (cachedWriteBuffer == null)
                {
                    MemoryStream stream = new MemoryStream();
                    using (XmlDictionaryWriter binaryWriter = XmlDictionaryWriter.CreateBinaryWriter(stream, XD.Dictionary))
                    {
                        OnWriteTo(binaryWriter);
                        binaryWriter.Flush();
                        stream.Flush();
                        stream.Seek(0, SeekOrigin.Begin);
                        cachedWriteBuffer = stream.GetBuffer();
                        cachedWriteBufferLength = (int)stream.Length;
                    }
                }
                writer.WriteNode(XmlDictionaryReader.CreateBinaryReader(cachedWriteBuffer, 0, cachedWriteBufferLength, XD.Dictionary, XmlDictionaryReaderQuotas.Max), false);
            }
            else
            {
                OnWriteTo(writer);
            }
        }

        public static RequestSecurityToken CreateFrom(XmlReader reader)
        {
            return CreateFrom(SecurityStandardsManager.DefaultInstance, reader);
        }

        public static RequestSecurityToken CreateFrom(XmlReader reader, MessageSecurityVersion messageSecurityVersion, SecurityTokenSerializer securityTokenSerializer)
        {
            return CreateFrom(SecurityUtils.CreateSecurityStandardsManager(messageSecurityVersion, securityTokenSerializer), reader);
        }

        internal static RequestSecurityToken CreateFrom(SecurityStandardsManager standardsManager, XmlReader reader)
        {
            return standardsManager.TrustDriver.CreateRequestSecurityToken(reader);
        }

        public void MakeReadOnly()
        {
            if (!isReadOnly)
            {
                isReadOnly = true;
                if (requestProperties != null)
                {
                    requestProperties = new ReadOnlyCollection<XmlElement>(requestProperties);
                }
                OnMakeReadOnly();
            }
        }

        internal protected virtual void OnWriteCustomAttributes(XmlWriter writer) { }

        internal protected virtual void OnWriteCustomElements(XmlWriter writer) { }

        internal protected virtual void OnMakeReadOnly() { }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            WriteTo(writer);
        }
    }
}
