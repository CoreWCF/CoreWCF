using System.IO;
using System.ServiceModel;

namespace ClientContract
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
