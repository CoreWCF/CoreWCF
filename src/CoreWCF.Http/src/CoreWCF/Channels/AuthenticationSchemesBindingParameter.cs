using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace CoreWCF.Channels
{
    internal class AuthenticationSchemesBindingParameter
    {
        public AuthenticationSchemesBindingParameter(AuthenticationSchemes authenticationSchemes)
        {
            Fx.Assert(authenticationSchemes != AuthenticationSchemes.None, "AuthenticationSchemesBindingParameter should not be added for AuthenticationSchemes.None.");

            AuthenticationSchemes = authenticationSchemes;
        }

        public AuthenticationSchemes AuthenticationSchemes { get; } = AuthenticationSchemes.None;

        public static bool TryExtract(BindingParameterCollection collection, out AuthenticationSchemes authenticationSchemes)
        {
            Fx.Assert(collection != null, "collection != null");
            authenticationSchemes = AuthenticationSchemes.None;
            AuthenticationSchemesBindingParameter instance = collection.Find<AuthenticationSchemesBindingParameter>();
            if (instance != null)
            {
                authenticationSchemes = instance.AuthenticationSchemes;
                return true;
            }

            return false;
        }
    }
}
