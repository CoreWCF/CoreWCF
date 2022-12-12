// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    }
}
