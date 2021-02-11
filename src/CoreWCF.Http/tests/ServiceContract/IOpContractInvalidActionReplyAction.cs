// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    internal interface IOpContractInvalidAction
    {
        [OperationContract(Action = null)]
        void TestMethodNullAction(int id);
    }

    [ServiceContract]
    internal interface IOpContractInvalidReplyAction
    {
        [OperationContract(ReplyAction = null)]
        int TestMethodNullReplyAction(int id);
    }
}
