// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Azure.Storage.Queues;

namespace CoreWCF.Channels
{
    public class AzureQueueStorageConnectionSettings
    {
        public static QueueClient GetQueueClientFromConnectionString(
            string connectionString,
            string queueName) {
            return new QueueClient(connectionString, queueName);
        }
    }
}
