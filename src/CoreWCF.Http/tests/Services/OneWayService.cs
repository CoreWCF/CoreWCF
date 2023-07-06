// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple,
        InstanceContextMode = InstanceContextMode.PerCall)]
    public class OneWayService : IOneWayContract
    {
        private readonly ConcurrentBag<string> _inputs;
        private readonly CountdownEvent _countdownEvent;

        public OneWayService(ConcurrentBag<string> inputs, CountdownEvent countdownEvent)
        {
            _inputs = inputs;
            _countdownEvent = countdownEvent;
        }

        public Task OneWay(string s) => Task.Factory.StartNew(() =>
        {
            _inputs.Add(s);
            _countdownEvent.Signal();
        });
    }
}
