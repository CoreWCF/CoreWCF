namespace CoreWCF
{
    public enum MessageCredentialType
    {
        None,
        Windows,
        UserName,
        Certificate,
        IssuedToken
    }

    internal static class MessageCredentialTypeHelper
    {
        internal static bool IsDefined(MessageCredentialType value)
        {
            return (value == MessageCredentialType.None ||
                value == MessageCredentialType.UserName ||
                value == MessageCredentialType.Windows ||
                value == MessageCredentialType.Certificate ||
                value == MessageCredentialType.IssuedToken);
        }
    }
}
