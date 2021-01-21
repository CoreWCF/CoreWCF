// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF;

namespace Services
{
    class ServiceWithMessageBodyAndHeader : IServiceWithMessageBodyAndHeader
    {
        public string Echo(string echo)
        {
            return echo;
        }

        public CoreEchoMessageResponse EchoWithMessageContract(CoreEchoMessageRequest request)
        {
            CoreEchoMessageResponse echoMessageResponse = new CoreEchoMessageResponse();
            echoMessageResponse.SayHello = "Saying Hello " + request.Text;
            echoMessageResponse.SayHi = "Saying Hi " + request.Text;
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
    }


    [MessageContract]
    public class CoreEchoMessageResponse
    {
        [MessageBodyMember]
        public string SayHello { get; set; }

        [MessageHeader]
        public string SayHi { get; set; }
    }
}
