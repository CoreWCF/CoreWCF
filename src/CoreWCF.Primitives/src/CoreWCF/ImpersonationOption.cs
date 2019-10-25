namespace CoreWCF
{
    public enum ImpersonationOption
    {
        NotAllowed,
        Allowed,
        Required,
    }

    static class ImpersonationOptionHelper
    {
        public static bool IsDefined(ImpersonationOption option)
        {
            return (option == ImpersonationOption.NotAllowed ||
                    option == ImpersonationOption.Allowed ||
                    option == ImpersonationOption.Required);
        }
    }
}
