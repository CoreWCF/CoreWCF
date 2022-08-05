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
            string queueName = uriLocalPath.Substring(
                uriLocalPath.IndexOf("private", StringComparison.InvariantCultureIgnoreCase) + 8,
                uriLocalPath.Length - uriLocalPath.LastIndexOf("/", StringComparison.InvariantCultureIgnoreCase) - 1);

            return $".\\Private$\\{queueName}";
        }

        public static string GetMsmqFormatQueueName(string queueName)
        {
            return $".\\Private$\\{queueName}";
        }
    }
}
