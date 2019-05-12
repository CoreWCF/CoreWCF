namespace CoreWCF.Channels
{
    public interface IOutputChannel : IChannel, ICommunicationObject
    {
        EndpointAddress RemoteAddress { get; }
        System.Uri Via { get; }
        System.Threading.Tasks.Task SendAsync(Message message);
        System.Threading.Tasks.Task SendAsync(Message message, System.Threading.CancellationToken token);
    }
}