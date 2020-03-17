using System.Net;

namespace CoreWCF.Channels
{
    internal static class AuthenticationSchemesHelper
    {
        public static bool IsSet(this AuthenticationSchemes thisPtr, AuthenticationSchemes authenticationSchemes)
        {
            return (thisPtr & authenticationSchemes) == authenticationSchemes;
        }

        public static bool IsNotSet(this AuthenticationSchemes thisPtr, AuthenticationSchemes authenticationSchemes)
        {
            return (thisPtr & authenticationSchemes) == 0;
        }
    }
}
