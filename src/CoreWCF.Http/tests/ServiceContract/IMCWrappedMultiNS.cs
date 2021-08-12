﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Name = "TStatic")]
    public interface IMCWrappedMultiNS
    {
        [OperationContract]
        [XmlSerializerFormat(Style = OperationFormatStyle.Document, Use = OperationFormatUse.Literal)]
        MC2MultiNS M(MCMultiNS msg);
    }

    [DataContract]
    public class DCMultiNS
    {
        [DataMember]
        public int a = 1;
    }
    [DataContract]
    public class DC2MultiNS
    {
        [DataMember]
        public int? b = new Nullable<int>(0);
    }
    [System.ServiceModel.MessageContract(WrapperName = "M", WrapperNamespace = "http://new")]
    public class MCMultiNS
    {
        [MessageBodyMember]
        public DCMultiNS dc;
        [MessageBodyMember]
        public int a = int.MaxValue;
        [System.ServiceModel.MessageProperty]
        public string e;
    }

    [MessageContract(WrapperName = "MResponse", WrapperNamespace = "http://new2")]
    public class MC2MultiNS
    {
        [MessageBodyMember]
        public DC2MultiNS dc2;
        [MessageBodyMember]
        public int a = int.MinValue;
        [System.ServiceModel.MessageProperty]
        public string e;
    }
}
