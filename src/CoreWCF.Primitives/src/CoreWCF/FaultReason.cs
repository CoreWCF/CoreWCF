using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CoreWCF
{
    public class FaultReason
    {
        private ReadOnlyCollection<FaultReasonText> _translations;

        public FaultReason(FaultReasonText translation)
        {
            if (translation == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(translation));

            Init(translation);
        }

        public FaultReason(string text)
        {
            // Let FaultReasonText constructor throw
            Init(new FaultReasonText(text));
        }

        internal FaultReason(string text, CultureInfo cultureInfo)
        {
            // Let FaultReasonText constructor throw
            Init(new FaultReasonText(text, cultureInfo));
        }

        public FaultReason(IEnumerable<FaultReasonText> translations)
        {
            if (translations == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(translations));
            int count = 0;
            foreach (FaultReasonText faultReasonText in translations)
                count++;
            if (count == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(translations),
                    SR.AtLeastOneFaultReasonMustBeSpecified);
            FaultReasonText[] array = new FaultReasonText[count];
            int index = 0;
            foreach (FaultReasonText faultReasonText in translations)
            {
                if (faultReasonText == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(translations),
                        SR.NoNullTranslations);

                array[index++] = faultReasonText;
            }
            Init(array);
        }

        private void Init(FaultReasonText translation)
        {
            Init(new FaultReasonText[] {translation});
        }

        private void Init(FaultReasonText[] translations)
        {
            _translations = new ReadOnlyCollection<FaultReasonText>(translations);
        }

        public FaultReasonText GetMatchingTranslation()
        {
            return GetMatchingTranslation(CultureInfo.CurrentCulture);
        }

        public FaultReasonText GetMatchingTranslation(CultureInfo cultureInfo)
        {
            if (cultureInfo == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(cultureInfo));

            // If there's only one translation, use it
            if (_translations.Count == 1)
                return _translations[0];

            // Search for an exact match
            for (int i = 0; i < _translations.Count; i++)
                if (_translations[i].Matches(cultureInfo))
                    return _translations[i];

            // If no exact match is found, proceed by looking for the a translation with a language that is a parent of the current culture

            if (_translations.Count == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentException(SR.NoMatchingTranslationFoundForFaultText));

            // Search for a more general language
            string localLang = cultureInfo.Name;
            while (true)
            {
                int idx = localLang.LastIndexOf('-');

                // We don't want to accept xml:lang=""
                if (idx == -1)
                    break;

                // Clip off the last subtag and look for a match
                localLang = localLang.Substring(0, idx);

                for (int i = 0; i < _translations.Count; i++)
                    if (_translations[i].XmlLang == localLang)
                        return _translations[i];
            }

            // Return the first translation if no match is found
            return _translations[0];
        }

        // public on full framework, but exposes a SynchronizedReadOnlyCollection
        internal ReadOnlyCollection<FaultReasonText> Translations
        {
            get { return _translations; }
        }

        public override string ToString()
        {
            if (_translations.Count == 0)
                return string.Empty;

            return GetMatchingTranslation().Text;

        }
    }
}