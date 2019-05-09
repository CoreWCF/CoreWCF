using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Primitives.Tests
{
    [ServiceContract]
    interface ISimpleService
    {
        [OperationContract]
        string Echo(string echo);
    }
}
