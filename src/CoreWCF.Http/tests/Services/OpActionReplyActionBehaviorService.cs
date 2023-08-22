// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CoreWCF.Channels;
using Xunit;

namespace Services
{
    public class OpActionReplyActionBehaviorService : ServiceContract.IOpActionReplyActionBehavior
    {
        private ConcurrentDictionary<Guid, TaskCompletionSource<string>> _testResults = new ConcurrentDictionary<Guid, TaskCompletionSource<string>>();
        public int TestMethodCheckCustomReplyAction(int id, string name)
        {
            Assert.Equal(1, id);
            Assert.Equal("Custom ReplyAction", name);
            return id + 1;
        }

        public int TestMethodCheckDefaultReplyAction(int id, string name)
        {
            Assert.Equal("Default ReplyAction", name);
            return id + 1;
        }

        public int TestMethodCheckEmptyReplyAction(int id, string name)
        {
            Assert.Equal("Empty ReplyAction", name);
            return id + 1;
        }

        public Message TestMethodCheckUntypedReplyAction()
        {
            Message serviceMessage = Message.CreateMessage(MessageVersion.Soap11, "myAction");
            return serviceMessage;
        }

        public int TestMethodCheckUriReplyAction(int id, string name)
        {
            Assert.Equal("Uri ReplyAction", name);
            return id + 1;
        }

        public void UnMatchedMessageHandler(Message m)
        {
            //bool flag = false;
            // Writing only the action of the message to the output
            string action = m.Headers.Action;
            if (string.IsNullOrEmpty(action))
            {
                action = "empty action";
            }
            int headerPos = m.Headers.FindHeader("TestId", "http://corewcf.net/");
            if (headerPos >= 0)
            {
                Guid testId = m.Headers.GetHeader<Guid>(headerPos);
                var tcs = _testResults.GetOrAdd(testId, new TaskCompletionSource<string>());
                tcs.SetResult(action);
            }
        }

        internal string GetTestResult(Guid testId)
        {
            try
            {
                var tcs = _testResults.GetOrAdd(testId, new TaskCompletionSource<string>());
                if (!tcs.Task.Wait(TimeSpan.FromSeconds(30)))
                {
                    return "Timeout waiting for value";
                }

                return tcs.Task.Result;
            }
            finally
            {
                _testResults.TryRemove(testId, out _);
            }
        }
    }
}
