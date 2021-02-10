// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace ClientContract
{
    [DataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    public class SampleServiceFault
    {
        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string ID { get; set; }
    }

    [ServiceContractAttribute]
    public interface IAggregateExceptionService
    {

        [OperationContractAttribute]
        [FaultContractAttribute(typeof(SampleServiceFault))]
        void SimpleOperationThrowingFault();

        [OperationContractAttribute]
        Task SimpleOperationThrowingFaultAsync();

        [OperationContractAttribute]
        [FaultContractAttribute(typeof(SampleServiceFault))]
        void ServiceOpWithMultipleTasks();

        [OperationContractAttribute]
        Task ServiceOpWithMultipleTasksAsync();

        [OperationContractAttribute]
        [FaultContractAttribute(typeof(SampleServiceFault))]
        void ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTask();

        [OperationContractAttribute]
        Task ServiceOpWithChainedTasks_ThrowFaultExceptionInOneTaskAsync();
    }
}
