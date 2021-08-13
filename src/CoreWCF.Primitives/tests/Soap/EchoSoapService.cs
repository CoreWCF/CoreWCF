// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Primitives.Tests.Soap
{
    [System.Serializable]
    [System.Xml.Serialization.SoapType]
    public class InnerComplexMessage : System.IEquatable<InnerComplexMessage>
    {
        private System.Guid _guid;

        public System.Guid Guid
        {
            get => _guid;
            set => _guid = value;
        }

        public bool Equals(InnerComplexMessage other)
            => other != null
                && _guid == other._guid;
    }

    [System.Serializable]
    [System.Xml.Serialization.SoapType]
    public class ComplexMessage : System.IEquatable<ComplexMessage>
    {
        private InnerComplexMessage[] _entitiesField;
        private System.DateTime _date;

        /// <remarks/>
        public InnerComplexMessage[] Entities
        {
            get => _entitiesField;
            set => _entitiesField = value;
        }

        /// <remarks/>
        public System.DateTime Date
        {
            get => _date;
            set => _date = value;
        }

        public bool Equals(ComplexMessage other)
            => other != null
                && _date == other._date
                && _entitiesField?.Length == other._entitiesField?.Length
                && System.Linq.Enumerable.SequenceEqual(_entitiesField, other._entitiesField);
    }

    [System.ServiceModel.ServiceContract]
    public interface IEchoSoapService
    {
        [System.ServiceModel.OperationContract]
        [System.ServiceModel.XmlSerializerFormat(Style = System.ServiceModel.OperationFormatStyle.Rpc, Use = System.ServiceModel.OperationFormatUse.Encoded)]
        [return: System.ServiceModel.MessageParameter(Name = "return")]
        string EchoEncoded(string echo);

        [System.ServiceModel.OperationContract]
        [System.ServiceModel.XmlSerializerFormat(Style = System.ServiceModel.OperationFormatStyle.Rpc, Use = System.ServiceModel.OperationFormatUse.Literal)]
        [return: System.ServiceModel.MessageParameter(Name = "return")]
        string EchoLiteral(string echo);

        [System.ServiceModel.OperationContract]
        [System.ServiceModel.XmlSerializerFormat(Style = System.ServiceModel.OperationFormatStyle.Rpc, Use = System.ServiceModel.OperationFormatUse.Encoded)]
        [System.ServiceModel.ServiceKnownType(typeof(ComplexMessage))]
        [System.ServiceModel.ServiceKnownType(typeof(InnerComplexMessage))]
        [return: System.ServiceModel.MessageParameter(Name = "return")]
        ComplexMessage GetComplexMessageEncoded([System.ServiceModel.MessageParameter(Name = "message")] ComplexMessage message);

        [System.ServiceModel.OperationContract]
        [System.ServiceModel.XmlSerializerFormat(Style = System.ServiceModel.OperationFormatStyle.Rpc, Use = System.ServiceModel.OperationFormatUse.Literal)]
        [System.ServiceModel.ServiceKnownType(typeof(ComplexMessage))]
        [System.ServiceModel.ServiceKnownType(typeof(InnerComplexMessage))]
        [return: System.ServiceModel.MessageParameter(Name = "return")]
        ComplexMessage GetComplexMessageLiteral([System.ServiceModel.MessageParameter(Name = "message")] ComplexMessage message);
    }

    public class EchoSoapService : IEchoSoapService
    {
        public string EchoEncoded(string echo)
            => echo;

        public string EchoLiteral(string echo)
           => echo;

        public ComplexMessage GetComplexMessageEncoded(ComplexMessage message)
            => message;

        public ComplexMessage GetComplexMessageLiteral(ComplexMessage message)
            => message;
    }
}
