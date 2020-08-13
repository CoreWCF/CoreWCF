using System.IO;
using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract]
    public interface IStream
    {
        [OperationContract]
        Stream Echo(Stream input);
    }
}
