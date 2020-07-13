using CoreWCF;
using ServiceContract;
using Xunit;

namespace Services
{
    [ServiceBehavior]
    public class ContractShapeOverloadsService : IServiceContract_Overloads
    {
        public string TwoWayMethod()
        {
            return string.Format("Server Received: Void");
        }

        public string TwoWayMethod(int n)
        {
            return string.Format("Server Received: {0}", n);
        }

        public string TwoWayMethod(string s)
        {
            return string.Format("Server Received: {0}", s);
        }

        public string TwoWayMethod(SM_ComplexType ct)
        {
            return string.Format("Server Received: {0} and {1}", ct.n, ct.s);
        }
    }

    [ServiceBehavior]
    public class ContractShapeParamsService : IServiceContract_Params
    {
        public string TwoWayParamArray(int n, params int[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != i)
                {
                    Assert.Equal(i, args[i]);
                }
            }

            return string.Format("Service recieved and processed {0} args", args.Length);
        }
    }
}