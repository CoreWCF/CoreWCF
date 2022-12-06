using System.Collections.Concurrent;
using System.Collections.Generic;
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

        public OneWayService(ConcurrentBag<string> inputs)
        {
            _inputs = inputs;
        }

        public Task OneWay(string s) => Task.Factory.StartNew(() =>
        {
            _inputs.Add(s);
        });
    }
}
