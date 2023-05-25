// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using CoreWCF;

namespace Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
    public class EchoService : Contract.IEchoService
    {
        private readonly ManualResetEvent _mre = new ManualResetEvent(false);

        public string EchoString(string echo)
        {
            return echo;
        }
    }
}
