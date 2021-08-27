using CoreWCF;
using ServiceContract;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Services
{
    [ServiceBehavior]
    public class OneWayService : IOneWayContract 
    {
        private ITestOutputHelper _output = new TestOutputHelper();

        public Task OneWay(string s)
        {
            Task task = new Task(delegate
            {
                _output.WriteLine(string.Format("Inoked oneway operation with {0}.", s));
            });
            task.Start();
            return task;
        }
    }
}
