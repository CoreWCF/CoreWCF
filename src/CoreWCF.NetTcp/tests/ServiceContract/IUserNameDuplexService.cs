// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace ServiceContract
{
    [CoreWCF.ServiceContract(Namespace = Constants.NS, CallbackContract = typeof(IUserNameDuplexCallback))]
    [System.ServiceModel.ServiceContract(Namespace = Constants.NS, CallbackContract = typeof(IUserNameDuplexCallback))]
    public interface IUserNameDuplexService
    {
        [CoreWCF.OperationContract]
        [CoreWCF.FaultContract(typeof(string))]
        [System.ServiceModel.OperationContract]
        [System.ServiceModel.FaultContract(typeof(string))]
        string Echo(string value);
    }

    [System.ServiceModel.ServiceContract(Name = nameof(IUserNameDuplexService), Namespace = Constants.NS,
        CallbackContract = typeof(IUserNameDuplexCallback))]
    public interface IAsyncUserNameDuplexService
    {
        [System.ServiceModel.OperationContract(Name = nameof(IUserNameDuplexService.Echo))]
        [System.ServiceModel.FaultContract(typeof(string))]
        Task<string> EchoAsync(string value);
    }

    [CoreWCF.ServiceContract(Namespace = Constants.NS)]
    [System.ServiceModel.ServiceContract(Namespace = Constants.NS)]
    public interface IUserNameDuplexCallback
    {
        [CoreWCF.OperationContract]
        [CoreWCF.FaultContract(typeof(string))]
        [System.ServiceModel.OperationContract]
        [System.ServiceModel.FaultContract(typeof(string))]
        string Echo(string value);
    }
}
