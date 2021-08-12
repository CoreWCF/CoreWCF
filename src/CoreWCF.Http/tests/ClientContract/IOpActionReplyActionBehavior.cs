// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;
using System.ServiceModel.Channels;

namespace ClientContract
{
    [ServiceContract(Name = "IOpActionReplyActionBehavior")]
    public interface IOpActionReplyActionBehavior
    {
        [OperationContract(IsOneWay = true)]
        void TestMethodCheckDefaultAction(int ID, string name);

        [OperationContract]
        int TestMethodCheckDefaultReplyAction(int ID, string name);

        [OperationContract(IsOneWay = true, Action = "myAction")]
        void TestMethodCheckCustomAction(int ID, string name);

        [OperationContract(ReplyAction = "myReplyAction")]
        int TestMethodCheckCustomReplyAction(int ID, string name);

        [OperationContract(IsOneWay = true, Action = "http://myAction")]
        void TestMethodCheckUriAction(int ID, string name);

        [OperationContract(ReplyAction = "http://myReplyAction")]
        int TestMethodCheckUriReplyAction(int ID, string name);

        [OperationContract(IsOneWay = true, Action = "")]
        void TestMethodCheckEmptyAction(int ID, string name);

        [OperationContract(ReplyAction = "")]
        int TestMethodCheckEmptyReplyAction(int ID, string name);

        [OperationContract(IsOneWay = true, Action = "*")]
        void TestMethodUntypedAction(Message m);

        [OperationContract(ReplyAction = "*")]
        Message TestMethodCheckUntypedReplyAction();

        [OperationContract(IsOneWay = true, Action = "*")]
        void UnMatchedMessageHandler(Message m);
    }
}
