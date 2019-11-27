using System;
using System.IO;

namespace Contract
{
    [CoreWCF.MessageContract]
    [System.ServiceModel.MessageContract]
    public class TestMessage
    {
        [CoreWCF.MessageHeader]
        [System.ServiceModel.MessageHeader]
        public String Header { get; set; }

        [CoreWCF.MessageBodyMember]
        [System.ServiceModel.MessageBodyMember]
        public Stream Body { get; set; }
    }
}
