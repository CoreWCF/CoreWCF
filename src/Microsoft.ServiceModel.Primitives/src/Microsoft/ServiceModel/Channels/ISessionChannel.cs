namespace Microsoft.ServiceModel.Channels
{
    public interface ISessionChannel<TSession> where TSession : ISession
    {
        TSession Session { get; }
    }
}