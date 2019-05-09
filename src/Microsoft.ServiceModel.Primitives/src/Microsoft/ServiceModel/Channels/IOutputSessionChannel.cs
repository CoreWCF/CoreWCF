namespace Microsoft.ServiceModel.Channels
{
    public interface IOutputSessionChannel : IChannel, IOutputChannel, ISessionChannel<IOutputSession>, ICommunicationObject
    {
    }
}