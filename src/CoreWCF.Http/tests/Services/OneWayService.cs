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
        public static ITestOutputHelper TestOutputHelper { get; set; }
        public static List<Task> Tasks { get; set; } = new();
        public static HashSet<string> Inputs { get; set; } = new();

        public void OneWay(string s)
        {
            var task = new Task(() =>
            {
                Inputs.Add(s);
            });
            Tasks.Add(task);
            task.Start();
        }
    }
}
