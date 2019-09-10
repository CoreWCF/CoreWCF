using System;
using System.IO;
using System.ServiceModel;

namespace ClientContract
{
    [MessageContract]
    public class TestMessage
    {
        [MessageHeader]
        public String Header { get; set; }
        [MessageBodyMember]
        public Stream Body { get; set; }
    }

}
