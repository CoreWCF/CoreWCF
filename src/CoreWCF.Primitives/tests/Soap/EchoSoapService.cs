// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Primitives.Tests.Soap
{
    [System.ServiceModel.ServiceContract]
    public interface IEchoSoapService
    {
        [System.ServiceModel.OperationContract]
        [System.ServiceModel.XmlSerializerFormat(Style = System.ServiceModel.OperationFormatStyle.Rpc)]
        [return: System.ServiceModel.MessageParameter(Name = "return")]
        string Echo(string echo);

        [System.ServiceModel.OperationContract]
        [System.ServiceModel.XmlSerializerFormat(Style = System.ServiceModel.OperationFormatStyle.Rpc, Use = System.ServiceModel.OperationFormatUse.Encoded)]
        [return: System.ServiceModel.MessageParameter(Name = "return")]
        string EchoEncoded(string echo);

        [System.ServiceModel.OperationContract]
        [System.ServiceModel.XmlSerializerFormat(Style = System.ServiceModel.OperationFormatStyle.Rpc, Use = System.ServiceModel.OperationFormatUse.Literal)]
        [return: System.ServiceModel.MessageParameter(Name = "return")]
        string EchoLiteral(string echo);
    }

    public class EchoSoapService : IEchoSoapService
    {
        public string Echo(string echo)
            => echo;

        public string EchoEncoded(string echo)
            => echo;

        public string EchoLiteral(string echo)
           => echo;
    }
}
