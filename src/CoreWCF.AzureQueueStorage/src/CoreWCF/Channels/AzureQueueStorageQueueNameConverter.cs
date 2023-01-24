// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    public class AzureQueueStorageQueueNameConverter
    {
        public static string GetEndpointUrl(string accountName, string queueName)
        {
            return $"https://{accountName}.queue.core.windows.net/{queueName}";
        }

        public static string GetAzureQueueStorageQueueName(Uri endpoint)
        {
            string uriLocalPath = endpoint.LocalPath; //check if this should be absolute path
            int startIndex = uriLocalPath.LastIndexOf("/", StringComparison.InvariantCultureIgnoreCase);
            int length = uriLocalPath.Length - uriLocalPath.LastIndexOf("/", StringComparison.InvariantCultureIgnoreCase) - 1;
            string queueName = uriLocalPath.Substring(startIndex, length);
            return queueName;
        }
    }
}
