// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract]
    public interface IServiceWithMessageBodyAndHeader
    {
        [OperationContract]
        string Echo(string echo);

        [OperationContract]
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
