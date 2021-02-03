// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;

namespace Contract
{
    internal static class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string TESTSERVICE_NAME = nameof(IEchoService);
        public const string OPERATION_BASE = NS + TESTSERVICE_NAME + "/";
    }

    [CoreWCF.ServiceContract(Namespace = Constants.NS, Name = Constants.TESTSERVICE_NAME)]
    [System.ServiceModel.ServiceContract(Namespace = Constants.NS, Name = Constants.TESTSERVICE_NAME)]
    public interface IEchoService
    {
        [CoreWCF.OperationContract(Name = "Echo", Action = Constants.OPERATION_BASE + "Echo",
            ReplyAction = Constants.OPERATION_BASE + "EchoResponse")]
        [System.ServiceModel.OperationContract(Name = "Echo", Action = Constants.OPERATION_BASE + "Echo",
            ReplyAction = Constants.OPERATION_BASE + "EchoResponse")]
        string EchoString(string echo);

        [CoreWCF.OperationContract(Name = "WaitForSecondRequest", Action = Constants.OPERATION_BASE + "WaitForSecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "WaitForSecondRequestResponse")]
        [System.ServiceModel.OperationContract(Name = "WaitForSecondRequest", Action = Constants.OPERATION_BASE + "WaitForSecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "WaitForSecondRequestResponse")]
        bool WaitForSecondRequest();

        [CoreWCF.OperationContract(Name = "WaitForSecondRequest", Action = Constants.OPERATION_BASE + "WaitForSecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "WaitForSecondRequestResponse")]
        [System.ServiceModel.OperationContract(Name = "WaitForSecondRequest", Action = Constants.OPERATION_BASE + "WaitForSecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "WaitForSecondRequestResponse")]
        Task<bool> WaitForSecondRequestAsync();


        [CoreWCF.OperationContract(Name = "SecondRequest", Action = Constants.OPERATION_BASE + "SecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "SecondRequestResponse")]
        [System.ServiceModel.OperationContract(Name = "SecondRequest", Action = Constants.OPERATION_BASE + "SecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "SecondRequestResponse")]
        void SecondRequest();

        [CoreWCF.OperationContract(Name = "GetClientIpEndpoint", Action = Constants.OPERATION_BASE + "GetClientIpEndpoint",
            ReplyAction = Constants.OPERATION_BASE + "GetClientIpEndpointResponse")]
        [System.ServiceModel.OperationContract(Name = "GetClientIpEndpoint", Action = Constants.OPERATION_BASE + "GetClientIpEndpoint",
            ReplyAction = Constants.OPERATION_BASE + "GetClientIpEndpointResponse")]
        string GetClientIpEndpoint();

        [CoreWCF.OperationContract(Name = "TestMessageContract", Action = Constants.OPERATION_BASE + "TestMessageContract",
            ReplyAction = Constants.OPERATION_BASE + "TestMessageContractResponse")]
        [System.ServiceModel.OperationContract(Name = "TestMessageContract", Action = Constants.OPERATION_BASE + "TestMessageContract",
            ReplyAction = Constants.OPERATION_BASE + "TestMessageContractResponse")]
        TestMessage TestMessageContract(TestMessage testMessage);
    }
}
