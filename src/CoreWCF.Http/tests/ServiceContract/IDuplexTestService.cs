// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Namespace = Constants.NS, Name = nameof(IDuplexTestCallback), SessionMode = SessionMode.Allowed)]
    public interface IDuplexTestCallback
    {
        [OperationContract(Name = "AddMessage", Action = Constants.NS + nameof(IDuplexTestService) + "/AddMessage",
            ReplyAction = Constants.NS + nameof(IDuplexTestService) + "/AddMessageResponse")]
        void AddMessage(string message);
    }

    [ServiceContract(Namespace = Constants.NS, Name = nameof(IDuplexTestService), CallbackContract = typeof(IDuplexTestCallback))]
    public interface IDuplexTestService
    {
        [OperationContract(Name = "EchoString", Action = Constants.NS + nameof(IDuplexTestService) + "/AddMessage",
            ReplyAction = Constants.NS + nameof(IDuplexTestService) + "/AddMessage")]
        string EchoString(string echo);
    }
}
