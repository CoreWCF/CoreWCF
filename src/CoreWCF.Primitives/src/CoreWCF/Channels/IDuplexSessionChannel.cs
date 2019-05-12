namespace CoreWCF.Channels
{
    public interface IDuplexSessionChannel : IChannel, IDuplexChannel, IInputChannel, IOutputChannel, ISessionChannel<IDuplexSession>, ICommunicationObject
    {
    }
}