// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace CoreWCF
{
    public class FaultReasonText
    {
        private readonly string _xmlLang;
        private readonly string _text;

        public FaultReasonText(string text)
        {
            if (text == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(text));
            _text = text;
            _xmlLang = CultureInfo.CurrentCulture.Name;
        }

        public FaultReasonText(string text, string xmlLang)
        {
            if (text == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(text));
            if (xmlLang == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(xmlLang));
            _text = text;
            _xmlLang = xmlLang;
        }

        // public on full framework
        internal FaultReasonText(string text, CultureInfo cultureInfo)
        {
            if (text == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(text));
            if (cultureInfo == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(cultureInfo));

            _text = text;
            _xmlLang = cultureInfo.Name;
        }

        public bool Matches(CultureInfo cultureInfo)
        {
            if (cultureInfo == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(cultureInfo));

            return _xmlLang == cultureInfo.Name;
        }

        public string XmlLang => _xmlLang;

        public string Text => _text;
    }
}