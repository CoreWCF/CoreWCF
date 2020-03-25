﻿using System;
using System.Collections.Generic;
using System.Text;


namespace CoreWCF.Primitives.Tests.Contracts
{
    [System.ServiceModel.MessageContract(IsWrapped = true,
                    WrapperName = "EchoMessageRequestObj",
                    WrapperNamespace = "http://EchoNamespace.org/Echo")]
    public class EchoMessageRequest
    {
        [System.ServiceModel.MessageBodyMember(Namespace = "http://EchoNamespace.org/Echo")]
        public string Text { get; set; }

        [System.ServiceModel.MessageHeader(Namespace = "http://EchoNamespace.org/Echo", Name = "DevKey")]
        public string APIKey { get; set; }
    }


    [System.ServiceModel.MessageContract(IsWrapped = true,
                    WrapperName = "EchoMessageResponseObj",
                    WrapperNamespace = "http://EchoNamespace.org/Echo")]
    public class EchoMessageResponse
    {
        [System.ServiceModel.MessageBodyMember(Namespace = "http://EchoNamespace.org/Echo")]
        public string SayHello { get; set; }

        [System.ServiceModel.MessageHeader(Namespace = "http://EchoNamespace.org/Echo")]
        public string SayHi { get; set; }
    }

}
