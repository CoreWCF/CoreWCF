using System;
using CoreWCF;

namespace ServiceContract
{

    [ServiceContract]
    public interface ISyncService
    {
        [OperationContract]
        string EchoString(string s);
    }
}
