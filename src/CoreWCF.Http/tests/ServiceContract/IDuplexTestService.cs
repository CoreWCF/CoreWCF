// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace ServiceContract
{
    internal static partial class Constants
    {
        public const string DUPLEX_TESTSERVICE_NAME = nameof(IDuplexTestService);
        public const string DUPLEX_TESTCALLBACK_NAME = nameof(IDuplexTestCallback);
        public const string DUPLEX_OPERATION_BASE = NS + DUPLEX_TESTSERVICE_NAME + "/";
    }

    [ServiceContract(Namespace = Constants.NS, Name = Constants.DUPLEX_TESTCALLBACK_NAME, SessionMode = SessionMode.Allowed)]
    public interface IDuplexTestCallback
    {
        [OperationContract(Name = "AddMessage", Action = Constants.DUPLEX_OPERATION_BASE + "AddMessage",
            ReplyAction = Constants.DUPLEX_OPERATION_BASE + "AddMessageResponse")]
        void AddMessage(string message);
    }

    [ServiceContract(Namespace = Constants.NS, Name = Constants.DUPLEX_TESTSERVICE_NAME, CallbackContract = typeof(IDuplexTestCallback))]
    public interface IDuplexTestService
    {
        [OperationContract(Name = "EchoString", Action = Constants.DUPLEX_OPERATION_BASE + "EchoString",
            ReplyAction = Constants.DUPLEX_OPERATION_BASE + "EchoStringResponse")]
        string EchoString(string echo);
    }
}
