// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        public string Header { get; set; }

        [CoreWCF.MessageBodyMember]
        [System.ServiceModel.MessageBodyMember]
        public Stream Body { get; set; }
    }
}
