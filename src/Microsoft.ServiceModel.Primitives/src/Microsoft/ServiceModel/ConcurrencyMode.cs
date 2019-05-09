namespace Microsoft.ServiceModel
{
    public enum ConcurrencyMode
    {
        Single, // This is first so it is ConcurrencyMode.default
        Reentrant,
        Multiple
    }

    internal static class ConcurrencyModeHelper
    {
        static public bool IsDefined(ConcurrencyMode x)
        {
            return
                x == ConcurrencyMode.Single ||
                x == ConcurrencyMode.Reentrant ||
                x == ConcurrencyMode.Multiple;
        }
    }
}