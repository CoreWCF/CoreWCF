using System;
using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract(Name = "ISyncService")]
    public interface IClientAsync_767311
    {
        [OperationContract(AsyncPattern = true)]
        IAsyncResult BeginEchoString(string s, AsyncCallback callback, object state);

        string EndEchoString(IAsyncResult result);
    }
}
