namespace Microsoft.ServiceModel.Description
{
    public enum MessageDirection
    {
        Input = 0,
        Output = 1,
    }

    static class MessageDirectionHelper
    {
        internal static bool IsDefined(MessageDirection value)
        {
            return (value == MessageDirection.Input || value == MessageDirection.Output);
        }

        internal static MessageDirection Opposite(MessageDirection d)
        {
            return d == MessageDirection.Input ? MessageDirection.Output : MessageDirection.Input;
        }
    }
}