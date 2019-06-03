namespace CoreWCF.Channels
{
    internal static class ChannelBindingMessagePropertyHelper
    {
        internal static bool TryGet(Message message, out ChannelBindingMessageProperty property)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }

            return TryGet(message.Properties, out property);
        }

        internal static bool TryGet(MessageProperties properties, out ChannelBindingMessageProperty property)
        {
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("properties");
            }

            property = null;
            object value;

            if (properties.TryGetValue(ChannelBindingMessageProperty.Name, out value))
            {
                property = value as ChannelBindingMessageProperty;
                return property != null;
            }

            return false;
        }

        internal static void AddTo(this ChannelBindingMessageProperty channelBindingProperty, Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }

            channelBindingProperty.AddTo(message.Properties);
        }

        internal static void AddTo(this ChannelBindingMessageProperty channelBindingProperty, MessageProperties properties)
        {
            // Throws if disposed
            var dummy = channelBindingProperty.ChannelBinding;
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("properties");
            }

            properties.Add(ChannelBindingMessageProperty.Name, channelBindingProperty);
        }
    }
}
