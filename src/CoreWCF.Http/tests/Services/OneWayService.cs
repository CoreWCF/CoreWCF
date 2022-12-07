using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using CoreWCF;
using ServiceContract;
using System.Threading.Tasks;
using Xunit.Abstractions;

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
