using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Primitives.Tests
{
    [ServiceContract]
    interface ISimpleService
    {
        [OperationContract]
        string Echo(string echo);
    }
}
