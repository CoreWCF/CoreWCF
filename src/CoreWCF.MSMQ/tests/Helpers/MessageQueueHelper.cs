// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MSMQ.Messaging;

namespace CoreWCF.MSMQ.Tests.Helpers
{
    internal class MessageQueueHelper
    {
        public static void Purge(string queueName)
        {
            var queue = new MessageQueue($".\\Private$\\{queueName}");

            queue.Purge();
        }

        public static void SendMessageInQueue(string queueName)
        {
            Stream stream = MessageContainer.GetTestMessage();
            var queue = new MessageQueue($".\\Private$\\{queueName}");
            var mess = new Message { BodyStream = stream, };
            queue.Send(mess);
        }

        public static void SendEmptyMessageInQueue(string queueName)
        {
            Stream stream = MessageContainer.GetEmptyTestMessage();
            var queue = new MessageQueue($".\\Private$\\{queueName}");
            var mess = new Message { BodyStream = stream, };
            queue.Send(mess);
        }

        public static void SendBadMessageInQueue(string queueName)
        {
            Stream stream = MessageContainer.GetBadTestMessage();
            var queue = new MessageQueue($".\\Private$\\{queueName}");
            var mess = new Message { BodyStream = stream, };
            queue.Send(mess);
        }

        public static async Task<bool> WaitMessageInDeadLetter()
        {
            string queueName = $"FormatName:DIRECT=OS:{System.Environment.MachineName}\\System$;Deadletter";
            var queue = new MessageQueue(queueName);

            var watch = Stopwatch.StartNew();
            var enumerator = queue.GetMessageEnumerator2();
            while (true)
            {
                if (enumerator.MoveNext())
                {
                    return true;
                }

                if (watch.Elapsed.TotalSeconds > 5)
                    return false;

                await Task.Delay(100);
            }
        }

        public static async Task<bool> WaitMessageInQueue(string queueName)
        {
            var queue = new MessageQueue($".\\Private$\\{queueName}");

            var watch = Stopwatch.StartNew();
            var enumerator = queue.GetMessageEnumerator2();
            while (true)
            {
                if (enumerator.MoveNext())
                {
                    return true;
                }

                if (watch.Elapsed.TotalSeconds > 5)
                    return false;

                await Task.Delay(100);
            }
        }

        public static void PurgeDeadLetter()
        {
            string queueName = $"FormatName:DIRECT=OS:{System.Environment.MachineName}\\System$;Deadletter";
            var queue = new MessageQueue(queueName);
            queue.Purge();
        }

        public static void SetRequirements(string queueName)
        {
            string nativeQueueName = $".\\Private$\\{queueName}";
            if (!MessageQueue.Exists(nativeQueueName))
            {
                MessageQueue.Create(nativeQueueName, false);
            }
        }
    }
}
