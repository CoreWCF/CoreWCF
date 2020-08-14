using System;
using CoreWCF;
using System.IO;

namespace ServiceContract
{
    [ServiceContract]
    public interface IVoidStreamService
    {
        [OperationContract(IsOneWay = true)]
        void Operation(Stream input);
    }

    [ServiceContract]
    [XmlSerializerFormat]
    public interface IVoidMyMessageService
    {
        [OperationContract(IsOneWay = true)]
        void Operation(MessageContractStreamNoHeader input);
    }

    [ServiceContract]
    public interface IStreamVoidService
    {
        [OperationContract(IsOneWay = false)]
        Stream Operation();
    }

    [ServiceContract]
    public interface IStreamStreamSyncService
    {
        [OperationContract()]
        Stream Operation(Stream input);
    }

    [ServiceContract]
    public interface IRefStreamService
    {
        [OperationContract()]
        void Operation(ref Stream input);
    }

    [ServiceContract]
    public interface IStreamInOutService
    {
        [OperationContract()]
        void Operation(Stream input, out Stream output);
    }

    [ServiceContract()]
    [XmlSerializerFormat]
    public interface IStreamStreamAsyncService
    {
        [OperationContract(AsyncPattern = true)]
        System.Threading.Tasks.Task<Stream> TwoWayMethodAsync(Stream input);
    }

    [ServiceContract]
    public interface IMessageContractStreamVoidService
    {
        [OperationContract(IsOneWay = false)]
        MessageContractStreamNoHeader Operation();
    }

    [ServiceContract]
    [XmlSerializerFormat]
    public interface IVoidMessageContractStreamService
    {
        [OperationContract(IsOneWay = false)]
        void Operation(MessageContractStreamNoHeader input);
    }

    [ServiceContract()]
    public interface IMessageContractStreamInReturnService
    {
        [OperationContract()]
        MessageContractStreamOneIntHeader Operation(MessageContractStreamNoHeader input);
    }

    [ServiceContract()]
    //[XmlSerializerFormat]
    public interface IMessageContractStreamMutipleOperationsService
    {
        [OperationContract]
        MessageContractStreamNoHeader Operation1(MessageContractStreamOneStringHeader input);
        [OperationContract]
        MessageContractStreamTwoHeaders Operation2(MessageContractStreamOneIntHeader input);
    }

    [MessageContract]
    public class MessageContractStreamNoHeader
    {
        [MessageBodyMember]
        public Stream stream = null;
    }

    [MessageContract]
    public class MessageContractStreamTwoHeaders
    {
        [MessageBodyMember]
        public Stream Stream { get; set; }

        [MessageHeader]
        public int intInHeader = 9;

        [MessageHeader]
        public string stringInHeader = "HELLO";
    }

    [MessageContract]
    public class MessageContractStreamOneIntHeader
    {
        [MessageBodyMember]
        public Stream input;

        [MessageHeader]
        public int count;
    }

    [MessageContract]
    public class MessageContractStreamOneStringHeader
    {
        [MessageBodyMember]
        public Stream input;

        [MessageHeader]
        public string count;
    }
}
