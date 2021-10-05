// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    internal abstract class SecurityHeaderElementInferenceEngine
    {
        public abstract ValueTask ExecuteProcessingPassesAsync(ReceiveSecurityHeader securityHeader, XmlDictionaryReader reader);

        public abstract void MarkElements(ReceiveSecurityHeaderElementManager elementManager, bool messageSecurityMode);

        public static SecurityHeaderElementInferenceEngine GetInferenceEngine(SecurityHeaderLayout layout)
        {
            SecurityHeaderLayoutHelper.Validate(layout);
            switch (layout)
            {
                case SecurityHeaderLayout.Strict:
                    return StrictModeSecurityHeaderElementInferenceEngine.Instance;
                case SecurityHeaderLayout.Lax:
                    return LaxModeSecurityHeaderElementInferenceEngine.Instance;
                case SecurityHeaderLayout.LaxTimestampFirst:
                    return LaxTimestampFirstModeSecurityHeaderElementInferenceEngine.Instance;
                case SecurityHeaderLayout.LaxTimestampLast:
                    return LaxTimestampLastModeSecurityHeaderElementInferenceEngine.Instance;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(layout)));
            }
        }
    }
}
