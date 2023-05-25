// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace ServiceContract
{
    internal static partial class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string TESTSERVICE_NAME = nameof(ITestService);
        public const string OPERATION_BASE = NS + TESTSERVICE_NAME + "/";
    }

    [ServiceContract(Namespace = Constants.NS, Name = Constants.TESTSERVICE_NAME)]
    public interface ITestService
    {
        [OperationContract(Name = "Echo", Action = Constants.OPERATION_BASE + "Echo",
            ReplyAction = Constants.OPERATION_BASE + "EchoResponse")]
        string EchoString(string echo);
    }
}
