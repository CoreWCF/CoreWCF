﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace Services
{
    internal class ServiceWithMessageBodyAndHeader : IServiceWithMessageBodyAndHeader
    {
        public string Echo(string echo)
        {
            return echo;
        }

        public CoreEchoMessageResponse EchoWithMessageContract(CoreEchoMessageRequest request)
        {
            CoreEchoMessageResponse echoMessageResponse = new CoreEchoMessageResponse
            {
                SayHello = "Saying Hello " + request.Text,
                SayHi = "Saying Hi " + request.Text,
                HeaderArrayValues = request.HeaderArrayValues,
            };
            return echoMessageResponse;
        }
    }

    [XmlSerializerFormat]
    [CoreWCF.ServiceContract]
    public interface IServiceWithMessageBodyAndHeader
    {
        [CoreWCF.OperationContract]
        string Echo(string echo);

        [CoreWCF.OperationContract]
        CoreEchoMessageResponse EchoWithMessageContract(CoreEchoMessageRequest request);
    }

    [MessageContract]
    public class CoreEchoMessageRequest
    {
        [MessageBodyMember]
        public string Text { get; set; }

        [MessageHeader]
        public string APIKey { get; set; }

        [MessageHeaderArray]
        public string[] HeaderArrayValues { get; set; }
    }


    [MessageContract]
    public class CoreEchoMessageResponse
    {
        [MessageBodyMember]
        public string SayHello { get; set; }

        [MessageHeader]
        public string SayHi { get; set; }

        [MessageHeaderArray]
        public string[] HeaderArrayValues { get; set; }
    }
}
