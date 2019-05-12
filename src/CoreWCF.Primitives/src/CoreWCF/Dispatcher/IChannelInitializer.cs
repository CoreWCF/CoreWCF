namespace CoreWCF.Dispatcher
{
    public interface IChannelInitializer
    {
        void Initialize(IClientChannel channel);
    }
}