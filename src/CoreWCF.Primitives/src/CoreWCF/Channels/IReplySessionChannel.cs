namespace CoreWCF.Channels
{
    public interface IReplySessionChannel : IReplyChannel, ISessionChannel<IInputSession>
    {
    }
}