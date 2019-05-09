namespace Microsoft.ServiceModel.Channels
{
    public interface IReplySessionChannel : IReplyChannel, ISessionChannel<IInputSession>
    {
    }
}