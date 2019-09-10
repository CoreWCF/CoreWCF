using CoreWCF;
using CoreWCF.Channels;
using ServiceContract;
using System;
using System.IO;
using System.Text;
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

        public string GetClientIpEndpoint()
        {
            if (OperationContext.Current.IncomingMessageProperties.TryGetValue(RemoteEndpointMessageProperty.Name, out object remoteEndpointObj))
            {
                var remoteEndpoint = remoteEndpointObj as RemoteEndpointMessageProperty;
                return remoteEndpoint.Address.ToString() + ":" + remoteEndpoint.Port.ToString();
            }

            throw new Exception("Remote endpoint message property not found");
        }

        public TestMessage TestMessageContract(TestMessage testMessage)
        {
            string text = new StreamReader(testMessage.Body, Encoding.UTF8).ReadToEnd();
            return new TestMessage()
            {
                Header = testMessage.Header + " from server",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(text + " from server"))
            };
        }
    }
}