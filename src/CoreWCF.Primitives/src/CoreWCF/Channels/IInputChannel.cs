namespace CoreWCF.Channels
{
    public interface IInputChannel : IChannel, ICommunicationObject
    {
        EndpointAddress LocalAddress { get; }
        System.Threading.Tasks.Task<Message> ReceiveAsync();
        System.Threading.Tasks.Task<Message> ReceiveAsync(System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<TryAsyncResult<Message>> TryReceiveAsync(System.Threading.CancellationToken token);
        System.Threading.Tasks.Task<bool> WaitForMessageAsync(System.Threading.CancellationToken token);
    }
}