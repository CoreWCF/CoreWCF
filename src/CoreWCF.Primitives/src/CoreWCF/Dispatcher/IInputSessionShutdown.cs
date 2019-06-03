namespace CoreWCF.Dispatcher
{
    internal interface IInputSessionShutdown
    {
        void ChannelFaulted(IDuplexContextChannel channel);
        void DoneReceiving(IDuplexContextChannel channel);
    }
}