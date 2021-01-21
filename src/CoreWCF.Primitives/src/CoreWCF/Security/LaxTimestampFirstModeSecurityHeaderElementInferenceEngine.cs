// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security
{
    internal sealed class LaxTimestampFirstModeSecurityHeaderElementInferenceEngine : LaxModeSecurityHeaderElementInferenceEngine
    {
        private LaxTimestampFirstModeSecurityHeaderElementInferenceEngine() { }

        internal new static LaxTimestampFirstModeSecurityHeaderElementInferenceEngine Instance { get; } = new LaxTimestampFirstModeSecurityHeaderElementInferenceEngine();

        public override void MarkElements(ReceiveSecurityHeaderElementManager elementManager, bool messageSecurityMode)
        {
            for (int position = 1; position < elementManager.Count; position++)
            {
                if (elementManager.GetElementCategory(position) == ReceiveSecurityHeaderElementCategory.Timestamp)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TimestampMustOccurFirstInSecurityHeaderLayout)));
                }
            }
            base.MarkElements(elementManager, messageSecurityMode);
        }
    }
}
