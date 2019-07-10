using CoreWCF;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Services
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
    public class TestService : ServiceContract.ITestService
    {
        private ManualResetEvent _mre = new ManualResetEvent(false);

        public string EchoString(string echo)
        {
            return echo;
        }

        public bool WaitForSecondRequest()
        {
            _mre.Reset();
            return _mre.WaitOne(TimeSpan.FromSeconds(10));
        }

        public void SecondRequest()
        {
            _mre.Set();
        }
    }
}