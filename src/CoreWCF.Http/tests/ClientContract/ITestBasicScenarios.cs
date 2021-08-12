// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;
using System.ServiceModel.Channels;

namespace ClientContract
{
    [ServiceContract]
    public interface ITestBasicScenarios
    {
        [OperationContract]
        string TestMethodDefaults(int ID, string name);

        [OperationContract(Action = "myAction")]
        void TestMethodSetAction(int ID, string name);

        [OperationContract(ReplyAction = "myReplyAction")]
        int TestMethodSetReplyAction(int ID, string name);

        [OperationContract(Action = "myUntypedAction")]
        void TestMethodUntypedAction(Message m);

        [OperationContract(ReplyAction = "myUntypedReplyAction")]
        Message TestMethodUntypedReplyAction();

        [OperationContract(Action = "*")]
        void TestMethodSetUntypedAction(Message m);
    }

    [ServiceContract(Name = "ITestBasicScenarios")]
    public interface ITestBasicScenariosClientService
    {
        [OperationContract(Action = "myAsyncAction", ReplyAction = "myAsyncReplyAction")]
        System.Threading.Tasks.Task<string> TestMethodAsync(int ID, string name);
    }
}
