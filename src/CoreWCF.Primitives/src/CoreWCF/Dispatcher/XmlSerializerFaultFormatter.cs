using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class XmlSerializerFaultFormatter : FaultFormatter
    {
        SynchronizedCollection<XmlSerializerOperationBehavior.Reflector.XmlSerializerFaultContractInfo> xmlSerializerFaultContractInfos;

        internal XmlSerializerFaultFormatter(Type[] detailTypes,
            SynchronizedCollection<XmlSerializerOperationBehavior.Reflector.XmlSerializerFaultContractInfo> xmlSerializerFaultContractInfos)
            : base(detailTypes)
        {
            Initialize(xmlSerializerFaultContractInfos);
        }

        internal XmlSerializerFaultFormatter(SynchronizedCollection<FaultContractInfo> faultContractInfoCollection,
            SynchronizedCollection<XmlSerializerOperationBehavior.Reflector.XmlSerializerFaultContractInfo> xmlSerializerFaultContractInfos)
            : base(faultContractInfoCollection)
        {
            Initialize(xmlSerializerFaultContractInfos);
        }

        void Initialize(SynchronizedCollection<XmlSerializerOperationBehavior.Reflector.XmlSerializerFaultContractInfo> xmlSerializerFaultContractInfos)
        {
            if (xmlSerializerFaultContractInfos == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(xmlSerializerFaultContractInfos));
            }
            this.xmlSerializerFaultContractInfos = xmlSerializerFaultContractInfos;
        }

        protected override XmlObjectSerializer GetSerializer(Type detailType, string faultExceptionAction, out string action)
        {
            action = faultExceptionAction;

            XmlSerializerOperationBehavior.Reflector.XmlSerializerFaultContractInfo faultInfo = null;
            for (int i = 0; i < xmlSerializerFaultContractInfos.Count; i++)
            {
                if (xmlSerializerFaultContractInfos[i].FaultContractInfo.Detail == detailType)
                {
                    faultInfo = xmlSerializerFaultContractInfos[i];
                    break;
                }
            }
            if (faultInfo != null)
            {
                if (action == null)
                    action = faultInfo.FaultContractInfo.Action;

                return faultInfo.Serializer;
            }
            else
                return new XmlSerializerObjectSerializer(detailType);
        }

        protected override FaultException CreateFaultException(MessageFault messageFault, string action)
        {
            IList<XmlSerializerOperationBehavior.Reflector.XmlSerializerFaultContractInfo> faultInfos;
            if (action != null)
            {
                faultInfos = new List<XmlSerializerOperationBehavior.Reflector.XmlSerializerFaultContractInfo>();
                for (int i = 0; i < xmlSerializerFaultContractInfos.Count; i++)
                {
                    if (xmlSerializerFaultContractInfos[i].FaultContractInfo.Action == action
                        || xmlSerializerFaultContractInfos[i].FaultContractInfo.Action == MessageHeaders.WildcardAction)
                    {
                        faultInfos.Add(xmlSerializerFaultContractInfos[i]);
                    }
                }
            }
            else
            {
                faultInfos = xmlSerializerFaultContractInfos;
            }

            Type detailType = null;
            object detailObj = null;
            for (int i = 0; i < faultInfos.Count; i++)
            {
                XmlSerializerOperationBehavior.Reflector.XmlSerializerFaultContractInfo faultInfo = faultInfos[i];
                XmlDictionaryReader detailReader = messageFault.GetReaderAtDetailContents();
                XmlObjectSerializer serializer = faultInfo.Serializer;

                if (serializer.IsStartObject(detailReader))
                {
                    detailType = faultInfo.FaultContractInfo.Detail;
                    try
                    {
                        detailObj = serializer.ReadObject(detailReader);
                        FaultException faultException = CreateFaultException(messageFault, action,
                            detailObj, detailType, detailReader);
                        if (faultException != null)
                            return faultException;
                    }
                    catch (SerializationException)
                    {
                    }
                }
            }
            return new FaultException(messageFault, action);
        }
    }

}