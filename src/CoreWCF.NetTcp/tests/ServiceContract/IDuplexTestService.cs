// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
    [System.ServiceModel.ServiceContract(Namespace = Constants.NS, Name = Constants.DUPLEX_TESTCALLBACK_NAME, SessionMode = System.ServiceModel.SessionMode.Allowed)]
    public interface IDuplexTestCallback
    {
        [OperationContract(Name = "AddMessage", Action = Constants.DUPLEX_OPERATION_BASE + "AddMessage",
            ReplyAction = Constants.DUPLEX_OPERATION_BASE + "AddMessageResponse")]
        [System.ServiceModel.OperationContract(Name = "AddMessage", Action = Constants.DUPLEX_OPERATION_BASE + "AddMessage",
            ReplyAction = Constants.DUPLEX_OPERATION_BASE + "AddMessageResponse")]
        void AddMessage(string message);
    }


    public class DuplexTestCallback : IDuplexTestCallback
    {
        public IList<string> ReceivedMessages { get; } = new List<string>();

        public void AddMessage(string message)
        {
            ReceivedMessages.Add(message);
        }
    }

    [ServiceContract(Namespace = Constants.NS, Name = Constants.DUPLEX_TESTSERVICE_NAME, CallbackContract = typeof(IDuplexTestCallback))]
    [System.ServiceModel.ServiceContract(Namespace = Constants.NS, Name = Constants.DUPLEX_TESTSERVICE_NAME, CallbackContract = typeof(IDuplexTestCallback))]
    public interface IDuplexTestService
    {
        [OperationContract(Name = "RegisterDuplexChannel", Action = Constants.DUPLEX_OPERATION_BASE + "Echo",
            ReplyAction = Constants.DUPLEX_OPERATION_BASE + "RegisterDuplexChannelResponse")]
        [System.ServiceModel.OperationContract(Name = "RegisterDuplexChannel", Action = Constants.DUPLEX_OPERATION_BASE + "Echo",
            ReplyAction = Constants.DUPLEX_OPERATION_BASE + "RegisterDuplexChannelResponse")]
        bool RegisterDuplexChannel();

        [OperationContract(Name = "SendMessage", Action = Constants.DUPLEX_OPERATION_BASE + "SendMessage",
            ReplyAction = Constants.DUPLEX_OPERATION_BASE + "SendMessageResponse")]
        [System.ServiceModel.OperationContract(Name = "SendMessage", Action = Constants.DUPLEX_OPERATION_BASE + "SendMessage",
            ReplyAction = Constants.DUPLEX_OPERATION_BASE + "SendMessageResponse")]
        void SendMessage(string message);
    }
}
