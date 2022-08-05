using CoreWCF;
using System;

namespace Contracts
{
    [ServiceContract]
    public interface ITestContract
    {
        [OperationContract(IsOneWay = true)]
        void Create(string name);
    }

    public class TestService : ITestContract
    {
        public void Create(string name)
        {
            Console.WriteLine($"Create: {name}");
        }
    }
}
