using System.IO;
using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract(Name = "Echo")]
    public interface IEcho
    {
        [OperationContract]
        Stream Echo(Stream input);
    }
    [ServiceContract(Name = "Forward")]
    public interface IForward
    {
        [OperationContract]
        Stream Forward(Stream input);
    }
}
