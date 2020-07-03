using System.IO;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface IStream
    {
        [OperationContract]
        Stream Echo(Stream input);
    }
}
