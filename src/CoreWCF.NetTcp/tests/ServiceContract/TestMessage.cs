using CoreWCF;
using System;
using System.IO;

namespace ServiceContract
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
