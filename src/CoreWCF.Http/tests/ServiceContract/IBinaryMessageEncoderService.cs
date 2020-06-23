using System.IO;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface IBinaryMessageEncoderService
    {
        [OperationContract]
        Stream GetStream();

        [OperationContract]
        string EchoString(string input);
    }
}
