namespace Microsoft.ServiceModel.Channels
{
    internal abstract class SessionOpenNotification
    {
        public abstract bool IsEnabled
        {
            get;
        }

        public abstract void UpdateMessageProperties(MessageProperties inboundMessageProperties);
    }
}