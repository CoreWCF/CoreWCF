namespace CoreWCF.Security
{
    sealed class LaxTimestampLastModeSecurityHeaderElementInferenceEngine : LaxModeSecurityHeaderElementInferenceEngine
    {
        static LaxTimestampLastModeSecurityHeaderElementInferenceEngine instance = new LaxTimestampLastModeSecurityHeaderElementInferenceEngine();

        LaxTimestampLastModeSecurityHeaderElementInferenceEngine() { }

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
