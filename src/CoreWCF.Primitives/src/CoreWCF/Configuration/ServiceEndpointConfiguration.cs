using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Configuration
{
    internal class ServiceEndpointConfiguration
    {
        public Uri Address { get; set; }
        public Binding Binding { get; set; }
        public Type Contract { get; set; }
        public Uri ListenUri { get; set; }
    }
}
