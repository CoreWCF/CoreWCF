// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace CoreWCF
{
    public class FaultReasonText
    {
        public FaultReasonText(string text)
        {
            Text = text ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(text));
            XmlLang = CultureInfo.CurrentCulture.Name;
        }

        public FaultReasonText(string text, string xmlLang)
        {
            Text = text ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(text));
            XmlLang = xmlLang ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(xmlLang));
        }

        // public on full framework
        internal FaultReasonText(string text, CultureInfo cultureInfo)
        {
            if (cultureInfo == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(cultureInfo));
            }

            Text = text ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(text));
            XmlLang = cultureInfo.Name;
        }

        public bool Matches(CultureInfo cultureInfo)
        {
            if (cultureInfo == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(cultureInfo));
            }

            return XmlLang == cultureInfo.Name;
        }

        public string XmlLang { get; }

        public string Text { get; }
    }
}