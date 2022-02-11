// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml.Serialization;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(ConfigurationName = "IWcfSoapService")]
    public interface IWcfSoapService
    {
        [OperationContract(Action = "http://tempuri.org/IWcfService/CombineStringXmlSerializerFormatSoap", ReplyAction = "http://tempuri.org/IWcfService/CombineStringXmlSerializerFormatSoapResponse")]
        [XmlSerializerFormat(Style = OperationFormatStyle.Rpc, SupportFaults = true, Use = OperationFormatUse.Encoded)]
        string CombineStringXmlSerializerFormatSoap(string message1, string message2);

        [OperationContract(Action = "http://tempuri.org/IWcfService/EchoComositeTypeXmlSerializerFormatSoap", ReplyAction = "http://tempuri.org/IWcfService/EchoComositeTypeXmlSerializerFormatSoapResponse")]
        [XmlSerializerFormat(Style = OperationFormatStyle.Rpc, SupportFaults = true, Use = OperationFormatUse.Encoded)]
        SoapComplexType EchoComositeTypeXmlSerializerFormatSoap(SoapComplexType c);

        [OperationContract(Action = "http://tempuri.org/IWcfService/ProcessCustomerData", ReplyAction = "http://tempuri.org/IWcfSoapService/ProcessCustomerDataResponse")]
        [XmlSerializerFormat(Style = OperationFormatStyle.Rpc, SupportFaults = true, Use = OperationFormatUse.Encoded)]
        [ServiceKnownType(typeof(AdditionalData))]
        [return: MessageParameter(Name = "ProcessCustomerDataReturn")]
        string ProcessCustomerData(CustomerObject CustomerData);

        [OperationContract(Action = "http://tempuri.org/IWcfService/Ping", ReplyAction = "http://tempuri.org/IWcfSoapService/PingResponse")]
        [XmlSerializerFormat(Style = OperationFormatStyle.Rpc, SupportFaults = true, Use = OperationFormatUse.Encoded)]
        PingEncodedResponse Ping(PingEncodedRequest request);
    }

    public class SoapComplexType
    {
        private bool _boolValue;
        private string _stringValue;

        public bool BoolValue
        {
            get { return _boolValue; }
            set { _boolValue = value; }
        }

        public string StringValue
        {
            get { return _stringValue; }
            set { _stringValue = value; }
        }
    }

    [MessageContract(WrapperName = "PingResponse", IsWrapped = true)]
    public class PingEncodedResponse
    {
        [MessageBodyMember(Namespace = "", Order = 0)]
        public int @Return;
    }

    [MessageContract(WrapperName = "Ping", IsWrapped = true)]
    public class PingEncodedRequest
    {
        [MessageBodyMember(Namespace = "", Order = 0)]
        public string Pinginfo;

        public PingEncodedRequest() { }

        public PingEncodedRequest(string pinginfo)
        {
            this.Pinginfo = pinginfo;
        }
    }

    [Serializable]
    [SoapType(Namespace = "WcfService")]
    public partial class AdditionalData
    {
        public string Field
        {
            get; set;
        }
    }

    [SoapType(Namespace = "WcfService")]
    public class CustomerObject
    {
        public string Name { get; set; }
        public object Data { get; set; }
    }
}
