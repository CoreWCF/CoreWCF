// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace ServiceContract
{
    internal static partial class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string TESTSERVICE_NAME = nameof(ITestService);
        public const string AUTHORIZATION_SERVICE_NAME = nameof(ITestAuthorizationService);
        public const string OPERATION_BASE = NS + TESTSERVICE_NAME + "/";
    }

    [ServiceContract(Namespace = Constants.NS, Name = Constants.AUTHORIZATION_SERVICE_NAME)]
    public interface ITestAuthorizationService
    {
        [OperationContract(Name = "EchoForAuthorizarionOneRole", Action = Constants.OPERATION_BASE + "EchoForAuthorizarionOneRole",
        ReplyAction = Constants.OPERATION_BASE + "EchoForAuthorizarionOneRoleResponse")]
        string EchoForAuthorizarionOneRole(string echo);

        [OperationContract(Name = "EchoForAuthorizarionNoRole", Action = Constants.OPERATION_BASE + "EchoForAuthorizarionNoRole",
        ReplyAction = Constants.OPERATION_BASE + "EchoForAuthorizarionNoRoleResponse")]
        string EchoForAuthorizarionNoRole(string echo);
    }

    [ServiceContract(Namespace = Constants.NS, Name = Constants.TESTSERVICE_NAME)]
    public interface ITestService : ITestAuthorizationService
    {
        [OperationContract(Name = "Echo", Action = Constants.OPERATION_BASE + "Echo",
            ReplyAction = Constants.OPERATION_BASE + "EchoResponse")]
        string EchoString(string echo);

        [OperationContract(Name = "WaitForSecondRequest", Action = Constants.OPERATION_BASE + "WaitForSecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "WaitForSecondRequestResponse")]
        bool WaitForSecondRequest();

        [OperationContract(Name = "SecondRequest", Action = Constants.OPERATION_BASE + "SecondRequest",
            ReplyAction = Constants.OPERATION_BASE + "SecondRequestResponse")]
        void SecondRequest();

        [OperationContract(Name = "GetClientIpEndpoint", Action = Constants.OPERATION_BASE + "GetClientIpEndpoint",
            ReplyAction = Constants.OPERATION_BASE + "GetClientIpEndpointResponse")]
        string GetClientIpEndpoint();

        [OperationContract(Name = "GetClientIpEndpointInjected", Action = Constants.OPERATION_BASE + "GetClientIpEndpointInjected",
            ReplyAction = Constants.OPERATION_BASE + "GetClientIpEndpointInjectedResponse")]
        string GetClientIpEndpointInjected();

        [OperationContract(Name = "TestMessageContract", Action = Constants.OPERATION_BASE + "TestMessageContract",
            ReplyAction = Constants.OPERATION_BASE + "TestMessageContractResponse")]
        TestMessage TestMessageContract(TestMessage testMessage);

        [OperationContract(Name = "EchoForPermission", Action = Constants.OPERATION_BASE + "EchoForPermission",
        ReplyAction = Constants.OPERATION_BASE + "EchoForPermissionResponse")]
        string EchoForPermission(string echo);

        [OperationContract(Name = "EchoForImpersonation", Action = Constants.OPERATION_BASE + "EchoForImpersonation",
        ReplyAction = Constants.OPERATION_BASE + "EchoForImpersonationResponse")]
        string EchoForImpersonation(string echo);
    }
}
