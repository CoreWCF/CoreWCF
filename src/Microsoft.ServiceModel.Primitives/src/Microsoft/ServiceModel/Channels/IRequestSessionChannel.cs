namespace Microsoft.ServiceModel.Channels
{
    public interface IRequestSessionChannel : IChannel, IRequestChannel, ISessionChannel<IOutputSession>, ICommunicationObject
    {
    }
}