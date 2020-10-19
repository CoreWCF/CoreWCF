using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;

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
