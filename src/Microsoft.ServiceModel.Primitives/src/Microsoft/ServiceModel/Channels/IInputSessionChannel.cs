namespace Microsoft.ServiceModel.Channels
{
    public interface IInputSessionChannel : IChannel, IInputChannel, ISessionChannel<IInputSession>, ICommunicationObject
    {
    }
}