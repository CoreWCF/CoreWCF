// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Services
{
    [CoreWCF.ServiceContract, CoreWCF.XmlSerializerFormat]
    public interface IServiceWithCoreWCFXmlSerializerFormat
    {
        [CoreWCF.OperationContract]
        string Identity(string msg);
    }

    public class ServiceWithCoreWCFXmlSerializerFormat : IServiceWithCoreWCFXmlSerializerFormat
    {
        public string Identity(string msg) => msg;
    }

    [System.ServiceModel.ServiceContract, System.ServiceModel.XmlSerializerFormat]
    public interface IServiceWithSSMXmlSerializerFormat
    {
        [System.ServiceModel.OperationContract]
        string Identity(string msg);
    }

    public class ServiceWithSSMXmlSerializerFormat : IServiceWithSSMXmlSerializerFormat
    {
        public string Identity(string msg) => msg;
    }

    [CoreWCF.ServiceContract]
    public interface IServiceWithCoreWCFXmlSerializerFormatOnOperation
    {
        [CoreWCF.OperationContract]
        [CoreWCF.XmlSerializerFormat]
        ComplexSerializableType Identity(ComplexSerializableType msg);
    }

    public class ServiceWithCoreWCFXmlSerializerFormatOnOperation : IServiceWithCoreWCFXmlSerializerFormatOnOperation
    {
        public ComplexSerializableType Identity(ComplexSerializableType msg) => msg;
    }

    [System.ServiceModel.ServiceContract]
    public interface IServiceWithSSMXmlSerializerFormatOnOperation
    {
        [System.ServiceModel.OperationContract]
        [System.ServiceModel.XmlSerializerFormat]
        ComplexSerializableType Identity(ComplexSerializableType msg);
    }

    public class ServiceWithSSMXmlSerializerFormatOnOperation : IServiceWithSSMXmlSerializerFormatOnOperation
    {
        public ComplexSerializableType Identity(ComplexSerializableType msg) => msg;
    }

    [Serializable]
    public class ComplexSerializableType
    {
        public string Message { get; set; }
    }
}
