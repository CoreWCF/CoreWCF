// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(ConfigurationName = "IWcfService")]
    public interface IWcfServiceXmlGenerated
    {
        [OperationContractAttribute(Action = "http://tempuri.org/IWcfService/EchoXmlSerializerFormat", ReplyAction = "http://tempuri.org/IWcfService/EchoXmlSerializerFormatResponse"),
        XmlSerializerFormat]
        string EchoXmlSerializerFormat(string message);

        [OperationContractAttribute(Action = "http://tempuri.org/IWcfService/EchoXmlSerializerFormatSupportFaults", ReplyAction = "http://tempuri.org/IWcfService/EchoXmlSerializerFormatSupportFaultsResponse"),
        XmlSerializerFormat(SupportFaults = true)]
        string EchoXmlSerializerFormatSupportFaults(string message, bool pleaseThrowException);

        [OperationContract(Action = "http://tempuri.org/IWcfService/EchoXmlSerializerFormatUsingRpc", ReplyAction = "http://tempuri.org/IWcfService/EchoXmlSerializerFormatUsingRpcResponse"),
        XmlSerializerFormat(Style = OperationFormatStyle.Rpc)]
        string EchoXmlSerializerFormatUsingRpc(string message);

        [OperationContract(Action = "http://tempuri.org/IWcfService/GetDataUsingXmlSerializer"),
        XmlSerializerFormat]
        XmlCompositeType GetDataUsingXmlSerializer(XmlCompositeType composite);

        [OperationContract(Action = "http://tempuri.org/IWcfService/EchoXmlVeryComplexType"),
        XmlSerializerFormat]
        XmlVeryComplexType EchoXmlVeryComplexType(XmlVeryComplexType complex);
    }

    public class XmlCompositeType
    {
        private bool _boolValue = true;
        private string _stringValue = "Hello ";

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

    public class XmlVeryComplexType
    {
        private int _id;
        private NonInstantiatedType _nonInstantiatedField = null;

        public NonInstantiatedType NonInstantiatedField
        {
            get
            {
                return _nonInstantiatedField;
            }
            set
            {
                _nonInstantiatedField = value;
            }
        }

        public int Id
        {
            get
            {
                return _id;
            }

            set
            {
                _id = value;
            }
        }
    }

    public class NonInstantiatedType
    {
    }
}

