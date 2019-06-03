namespace CoreWCF
{
    public enum TcpClientCredentialType
    {
        None = 0,
        Windows = 1,
        Certificate = 2,
    }

    internal static class TcpClientCredentialTypeHelper
    {
        internal static bool IsDefined(TcpClientCredentialType value)
        {
            return (value == TcpClientCredentialType.None ||
                value == TcpClientCredentialType.Windows ||
                value == TcpClientCredentialType.Certificate);
        }
    }
}