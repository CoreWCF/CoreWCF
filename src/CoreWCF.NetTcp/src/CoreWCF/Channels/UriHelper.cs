using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    internal static class UriHelper
    {
        internal static void ValidateBaseAddress(Uri uri, string argumentName)
        {
            if (uri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(argumentName);
            }

            if (!uri.IsAbsoluteUri)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argumentName, SR.BaseAddressMustBeAbsolute);
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argumentName, SR.BaseAddressCannotHaveUserInfo);
            }

            if (!string.IsNullOrEmpty(uri.Query))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argumentName, SR.BaseAddressCannotHaveQuery);
            }

            if (!string.IsNullOrEmpty(uri.Fragment))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(argumentName, SR.BaseAddressCannotHaveFragment);
            }
        }
    }
}
