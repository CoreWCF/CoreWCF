namespace CoreWCF.Channels
{
    public interface IRequestSessionChannel : IChannel, IRequestChannel, ISessionChannel<IOutputSession>, ICommunicationObject
    {
    }
}