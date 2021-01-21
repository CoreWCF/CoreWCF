// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security
{
    internal sealed class LaxTimestampLastModeSecurityHeaderElementInferenceEngine : LaxModeSecurityHeaderElementInferenceEngine
    {
        private static readonly LaxTimestampLastModeSecurityHeaderElementInferenceEngine instance = new LaxTimestampLastModeSecurityHeaderElementInferenceEngine();

        private LaxTimestampLastModeSecurityHeaderElementInferenceEngine() { }

        internal new static LaxTimestampLastModeSecurityHeaderElementInferenceEngine Instance
        {
            get { return instance; }
        }

        public override void MarkElements(ReceiveSecurityHeaderElementManager elementManager, bool messageSecurityMode)
        {
            for (int position = 0; position < elementManager.Count - 1; position++)
            {
                if (elementManager.GetElementCategory(position) == ReceiveSecurityHeaderElementCategory.Timestamp)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.TimestampMustOccurLastInSecurityHeaderLayout)));
                }
            }
            base.MarkElements(elementManager, messageSecurityMode);
        }
    }
}
