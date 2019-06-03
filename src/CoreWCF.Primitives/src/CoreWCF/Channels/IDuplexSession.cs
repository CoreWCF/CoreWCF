namespace CoreWCF.Channels
{
    public interface IDuplexSession : IInputSession, IOutputSession, ISession
    {
        System.Threading.Tasks.Task CloseOutputSessionAsync();
        System.Threading.Tasks.Task CloseOutputSessionAsync(System.Threading.CancellationToken token);
    }
}