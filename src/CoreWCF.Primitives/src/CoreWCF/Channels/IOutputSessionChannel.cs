namespace CoreWCF.Channels
{
    public interface IOutputSessionChannel : IChannel, IOutputChannel, ISessionChannel<IOutputSession>, ICommunicationObject
    {
    }
}