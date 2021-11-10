// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;

namespace ClientContract
{
    [System.Runtime.Serialization.DataContract]
    public class SSMCompatibilityFault
    {
        public const string Name = nameof(SSMCompatibilityFault);
        public const string Namespace = "https://ssm-fault-contract-compatibility.com";

        [System.Runtime.Serialization.DataMember]
        public string Message { get; set; }
    }

    [ServiceContract]
    public interface IServiceWithCoreWCFFaultContract
    {
        [OperationContract]
        [FaultContract(typeof(SSMCompatibilityFault), Name = SSMCompatibilityFault.Name, Namespace = SSMCompatibilityFault.Namespace)]
        string Identity(string msg);
    }

    [ServiceContract(Namespace = SSMCompatibilityFault.Namespace)]
    public interface IServiceWithCoreWCFFaultContractWithNamespaceAtServiceContractLevel
    {
        [OperationContract]
        [FaultContract(typeof(SSMCompatibilityFault), Name = SSMCompatibilityFault.Name)]
        string Identity(string msg);
    }

    [ServiceContract]
    public interface IServiceWithSSMFaultContract
    {
        [OperationContract]
        [FaultContract(typeof(SSMCompatibilityFault), Name = SSMCompatibilityFault.Name, Namespace = SSMCompatibilityFault.Namespace)]
        string Identity(string msg);
    }

    [ServiceContract(Namespace = SSMCompatibilityFault.Namespace)]
    public interface IServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel
    {
        [OperationContract]
        [FaultContract(typeof(SSMCompatibilityFault), Name = SSMCompatibilityFault.Name)]
        string Identity(string msg);
    }
}
