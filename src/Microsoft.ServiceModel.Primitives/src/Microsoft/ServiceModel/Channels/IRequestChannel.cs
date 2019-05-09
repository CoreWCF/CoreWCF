namespace Microsoft.ServiceModel.Channels
{
    public interface IRequestChannel : IChannel, ICommunicationObject
    {
        EndpointAddress RemoteAddress { get; }
        System.Uri Via { get; }
        System.Threading.Tasks.Task<Message> RequestAsync(Message message);
        System.Threading.Tasks.Task<Message> RequestAsync(Message message, System.Threading.CancellationToken token);
    }
}