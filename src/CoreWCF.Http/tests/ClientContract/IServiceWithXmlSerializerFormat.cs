// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract, XmlSerializerFormat]
    public interface IServiceWithCoreWCFXmlSerializerFormat
    {
        [OperationContract]
        string Identity(string msg);
    }

    [ServiceContract, XmlSerializerFormat]
    public interface IServiceWithSSMXmlSerializerFormat
    {
        [OperationContract]
        string Identity(string msg);
    }

    [ServiceContract]
    public interface IServiceWithCoreWCFXmlSerializerFormatOnOperation
    {
        [OperationContract]
        [XmlSerializerFormat]
        ComplexSerializableType Identity(ComplexSerializableType msg);
    }

    [ServiceContract]
    public interface IServiceWithSSMXmlSerializerFormatOnOperation
    {
        [OperationContract]
        [XmlSerializerFormat]
        ComplexSerializableType Identity(ComplexSerializableType msg);
    }

    [Serializable]
    public class ComplexSerializableType
    {
        public string Message { get; set; }
    }
}
