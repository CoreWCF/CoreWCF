// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF;
using Xunit;

namespace Services
{
    [ServiceBehavior]
    public class TestBasicScenariosService : ServiceContract.ITestBasicScenarios
    {
        private delegate string myDelegate(int ID, string name);
        public async Task<string> TestMethodAsync(int ID, string name)
        {
            myDelegate del = ProcessAsync;
            var workTask = System.Threading.Tasks.Task.Run(() => del.Invoke(ID, name));
            return await workTask;
        }

        public string ProcessAsync(int ID, string name)
        {
            return name;
        }

        public string TestMethodDefaults(int ID, string name)
        {
            return name;
        }

        public void TestMethodSetAction(int ID, string name)
        {
            Assert.NotNull(name);
            Assert.Equal("Action", name);
        }

        public int TestMethodSetReplyAction(int ID, string name)
        {
            return ID;
        }

        public void TestMethodSetUntypedAction(CoreWCF.Channels.Message msg)
        {
            Assert.NotNull(msg);
        }

        public void TestMethodUntypedAction(CoreWCF.Channels.Message msg)
        {
            Assert.NotNull(msg);
        }

        public CoreWCF.Channels.Message TestMethodUntypedReplyAction()
        {
            CoreWCF.Channels.Message serviceMessage = CoreWCF.Channels.Message.CreateMessage(CoreWCF.Channels.MessageVersion.Soap11, "myUntypedReplyAction");
            return serviceMessage;
        }
    }
}

