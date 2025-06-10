// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class DuplexTestService : IDuplexTestService
    {
        public string EchoString(string echo)
        {
            return echo;
        }
    }
}
