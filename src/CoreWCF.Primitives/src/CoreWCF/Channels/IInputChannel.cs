namespace CoreWCF.Channels
{
    public interface IInputChannel : IChannel, ICommunicationObject
    {
        EndpointAddress LocalAddress { get; }
    }
}