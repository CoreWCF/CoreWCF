// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Services
{
    [System.Runtime.Serialization.DataContract]
    public class SSMCompatibilityFault
    {
        public const string Name = nameof(SSMCompatibilityFault);
        public const string Namespace = "https://ssm-fault-contract-compatibility.com";

        [System.Runtime.Serialization.DataMember]
        public string Message { get; set; }
    }

    [CoreWCF.ServiceContract]
    public interface IServiceWithCoreWCFFaultContract
    {
        [CoreWCF.OperationContract]
        [CoreWCF.FaultContract(typeof(SSMCompatibilityFault), Name = SSMCompatibilityFault.Name, Namespace = SSMCompatibilityFault.Namespace)]
        string Identity(string msg);
    }

    public class ServiceWithCoreWCFFaultContract : IServiceWithCoreWCFFaultContract
    {
        public string Identity(string msg) => throw new CoreWCF.FaultException<SSMCompatibilityFault>(new SSMCompatibilityFault()
        {
            Message = "An error occured"
        });
    }

    [CoreWCF.ServiceContract(Namespace = SSMCompatibilityFault.Namespace)]
    public interface IServiceWithCoreWCFFaultContractWithNamespaceAtServiceContractLevel
    {
        [CoreWCF.OperationContract]
        [CoreWCF.FaultContract(typeof(SSMCompatibilityFault), Name = SSMCompatibilityFault.Name)]
        string Identity(string msg);
    }

    public class ServiceWithCoreWCFFaultContractWithNamespaceAtServiceContractLevel : IServiceWithCoreWCFFaultContractWithNamespaceAtServiceContractLevel
    {
        public string Identity(string msg) => throw new CoreWCF.FaultException<SSMCompatibilityFault>(new SSMCompatibilityFault()
        {
            Message = "An error occured"
        });
    }

    [System.ServiceModel.ServiceContract]
    public interface IServiceWithSSMFaultContract
    {
        [System.ServiceModel.OperationContract]
        [System.ServiceModel.FaultContract(typeof(SSMCompatibilityFault), Name = SSMCompatibilityFault.Name, Namespace = SSMCompatibilityFault.Namespace)]
        string Identity(string msg);
    }

    public class ServiceWithSSMFaultContract : IServiceWithSSMFaultContract
    {
        public string Identity(string msg) => throw new CoreWCF.FaultException<SSMCompatibilityFault>(new SSMCompatibilityFault()
        {
            Message = "An error occured"
        });
    }

    [System.ServiceModel.ServiceContract(Namespace = SSMCompatibilityFault.Namespace)]
    public interface IServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel
    {
        [System.ServiceModel.OperationContract]
        [System.ServiceModel.FaultContract(typeof(SSMCompatibilityFault), Name = SSMCompatibilityFault.Name)]
        string Identity(string msg);
    }

    public class ServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel : IServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel
    {
        public string Identity(string msg) => throw new CoreWCF.FaultException<SSMCompatibilityFault>(new SSMCompatibilityFault()
        {
            Message = "An error occured"
        });
    }
}
