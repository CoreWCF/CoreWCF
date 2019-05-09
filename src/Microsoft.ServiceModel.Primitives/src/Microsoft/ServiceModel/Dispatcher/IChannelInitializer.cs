namespace Microsoft.ServiceModel.Dispatcher
{
    public interface IChannelInitializer
    {
        void Initialize(IClientChannel channel);
    }
}