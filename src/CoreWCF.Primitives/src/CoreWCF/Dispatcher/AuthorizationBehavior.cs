using System;
using System.Globalization;

namespace CoreWCF.Dispatcher
{
    public class AuthorizationBehavior
    {
        // These values comes from SecurityJan2004Dictionary but there's a lot of boiler plate code to get this value so I've put these values
        // directly here to avoid pulling in extra code for now.
        internal const string FailedAuthenticationFaultCode = "FailedAuthentication";
        internal const string HeaderNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

        internal static Exception CreateAccessDeniedFaultException()
        {
            // always use default version?
            //SecurityVersion wss = SecurityVersion.Default;
            //FaultCode faultCode = FaultCode.CreateSenderFaultCode(wss.FailedAuthenticationFaultCode.Value, wss.HeaderNamespace.Value);
            FaultCode faultCode = FaultCode.CreateSenderFaultCode(FailedAuthenticationFaultCode, HeaderNamespace);
            FaultReason faultReason = new FaultReason(SR.AccessDenied, CultureInfo.CurrentCulture);
            return new FaultException(faultReason, faultCode);
        }
    }
}