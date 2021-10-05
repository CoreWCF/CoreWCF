// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Xml
{
    internal static class XmlConverter
    {
        public static bool IsWhitespace(char ch)
        {
            return (ch <= ' ' && (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n'));
        }

        public static bool IsWhitespace(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (!IsWhitespace(s[i]))
                    return false;
            }

            return true;
        }
    }
}
