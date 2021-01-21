// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace Contract
{
    [CoreWCF.ServiceContract]
    [System.ServiceModel.ServiceContract]
    public interface ITaskService
    {
        [CoreWCF.OperationContract]
        [System.ServiceModel.OperationContract]
        Task SynchronousCompletion();

        [CoreWCF.OperationContract]
        [System.ServiceModel.OperationContract]
        Task AsynchronousCompletion();
    }
}
