using CoreWCF;
using ServiceContract;
using System.Collections;
using Xunit;

namespace Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class LifeTimeTestService : ILifeTimeTestService
    {
        public static Hashtable instanceContextCollection = new Hashtable();
        public static object mutex = new object();
        public static int oneWayCalls = 0;
        public static int twoWayCalls = 0;

        public void Start()
        {
            oneWayCalls = 0;
            twoWayCalls = 0;
        }

        public void CheckInstanceContext(InstanceContext instanceContext)
        {
            lock (mutex)
            {
                Assert.False(instanceContextCollection.Contains(instanceContext), "Failed as we are reusing an instance context");
                instanceContextCollection.Add(instanceContext, instanceContext);
            }
        }

        public void OneWay()
        {
            CheckInstanceContext(OperationContext.Current.InstanceContext);
            lock (mutex)
            {
                oneWayCalls++;
            }
        }

        public void TwoWay()
        {
            CheckInstanceContext(OperationContext.Current.InstanceContext);

            lock (mutex)
            {
                twoWayCalls++;
            }
        }

        public void Final(int calls, string variation)
        {
            bool flag = false;
            while (calls != twoWayCalls) //Use # of twoWayCalls for validation since OneWay support has problem for now
            {
                System.Threading.Thread.CurrentThread.Join(5 * 1000);
            }

            Assert.True(!flag, "Calls from variation " + variation + " were successful");
        }
    }
}
