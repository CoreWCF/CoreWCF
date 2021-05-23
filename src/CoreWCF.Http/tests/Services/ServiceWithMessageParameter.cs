// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Services
{
    [CoreWCF.ServiceContract]
    public interface IServiceWithCoreWCFMessageParameter
    {
        [CoreWCF.OperationContract]
        [return: CoreWCF.MessageParameter(Name = "Output")]
        string Identity([CoreWCF.MessageParameter(Name = "Input")] string msg);
    }

    public class ServiceWithCoreWCFMessageParameter : IServiceWithCoreWCFMessageParameter
    {
        public string Identity(string msg) => msg;
    }

    [System.ServiceModel.ServiceContract]
    public interface IServiceWithSSMMessageParameter
    {
        [System.ServiceModel.OperationContract]
        [return: System.ServiceModel.MessageParameter(Name = "Output")]
        string Identity([System.ServiceModel.MessageParameter(Name = "Input")] string msg);
    }

    public class ServiceWithSSMMessageParameter : IServiceWithSSMMessageParameter
    {
        public string Identity(string msg) => msg;
    }
}
