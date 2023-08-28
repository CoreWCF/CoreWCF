﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using CoreWCF;

namespace ServiceContract
{
    [MessageContract]
    public class TestMessage
    {
        [MessageHeader]
        public string Header { get; set; }
        [MessageBodyMember]
        public Stream Body { get; set; }
    }
}
