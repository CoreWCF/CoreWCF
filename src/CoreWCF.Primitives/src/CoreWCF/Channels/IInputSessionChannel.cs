namespace CoreWCF.Channels
{
    public interface IInputSessionChannel : IChannel, IInputChannel, ISessionChannel<IInputSession>, ICommunicationObject
    {
    }
}