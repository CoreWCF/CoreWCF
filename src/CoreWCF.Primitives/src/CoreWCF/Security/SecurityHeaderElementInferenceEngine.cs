using CoreWCF.Security.Tokens;
using CoreWCF.Channels;
using System.Xml;
using System;

namespace CoreWCF.Security
{

    abstract class SecurityHeaderElementInferenceEngine
    {
        public abstract void ExecuteProcessingPasses(ReceiveSecurityHeader securityHeader, XmlDictionaryReader reader);

      //  public abstract void MarkElements(ReceiveSecurityHeaderElementManager elementManager, bool messageSecurityMode);

        public static SecurityHeaderElementInferenceEngine GetInferenceEngine(SecurityHeaderLayout layout)
        {
            SecurityHeaderLayoutHelper.Validate(layout);

            switch (layout)
            {
                case SecurityHeaderLayout.Strict:
                    return StrictModeSecurityHeaderElementInferenceEngine.Instance;
                //TODO (Check)
                //case SecurityHeaderLayout.Lax:
                //    return LaxModeSecurityHeaderElementInferenceEngine.Instance;
                //case SecurityHeaderLayout.LaxTimestampFirst:
                //    return LaxTimestampFirstModeSecurityHeaderElementInferenceEngine.Instance;
                //case SecurityHeaderLayout.LaxTimestampLast:
                //    return LaxTimestampLastModeSecurityHeaderElementInferenceEngine.Instance;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("layout"));
            }
        }
    }
}
