using System.IO;
using CoreWCF;

namespace ServiceContract
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
