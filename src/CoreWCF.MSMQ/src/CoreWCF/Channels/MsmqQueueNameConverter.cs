// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    public class MsmqQueueNameConverter
    {
        public static string GetEndpointUrl(string queueName)
{
            return $"net.msmq://localhost/private/{queueName}";
        }

        public static string GetMsmqFormatQueueName(Uri endpoint)
        {
            string uriLocalPath = endpoint.LocalPath;
            int startIndex = uriLocalPath.IndexOf("private", StringComparison.InvariantCultureIgnoreCase) + 8;
            int length = uriLocalPath.Length - uriLocalPath.LastIndexOf("/", StringComparison.InvariantCultureIgnoreCase) - 1;
            string queueName = uriLocalPath.Substring(startIndex, length);
            return $".\\Private$\\{queueName}";
        }
    }
}
