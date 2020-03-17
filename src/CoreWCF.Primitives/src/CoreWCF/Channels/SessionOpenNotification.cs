namespace CoreWCF.Channels
{
    public abstract class SessionOpenNotification
    {
        public abstract bool IsEnabled { get; }
        public abstract void UpdateMessageProperties(MessageProperties inboundMessageProperties);
    }
}