// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Services
{
    public class OpContractInvalidActionSerivce : ServiceContract.IOpContractInvalidAction
    {
        public void TestMethodNullAction(int id)
        {
            Assert.False(true, $"Parameter in: {id}, but service should throw before reaching here.");
        }
    }

    public class OpContractInvalidReplyActionSerivce : ServiceContract.IOpContractInvalidReplyAction
    {
        public int TestMethodNullReplyAction(int id)
        {
            Assert.False(true, $"Parameter in: {id}, but service should throw before reaching here.");
            return id;
        }
    }
}
