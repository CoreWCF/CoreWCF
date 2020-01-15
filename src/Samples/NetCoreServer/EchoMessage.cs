using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace NetCoreServer
{
    [DataContract]
    public class EchoMessage
    {
        public string Text { get; set; }
    }
}
