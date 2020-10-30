using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace ServiceContract
{
    [ServiceContract(Name = "ISampleServiceTaskServerside")]
    public interface ISampleServiceTaskServerside
    {
        [OperationContract]
        Task<List<Book>> SampleMethodAsync(string name, string publisher);

        [OperationContract]
        Task SampleMethodAsync2(string name);
    }
}
