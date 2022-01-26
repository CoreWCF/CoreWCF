// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using CoreWCF.Channels;

namespace CoreWCF
{
    internal static class ContentTypeHelpers
    {
        internal static Encoding GetEncodingFromContentType(string contentType, ContentEncoding[] contentMap)
        {
            if (contentType == null)
            {
                return null;
            }

            // Check for known/expected content types
            for (int i = 0; i < contentMap.Length; i++)
            {
                if (contentMap[i].contentType == contentType)
                {
                    return contentMap[i].encoding;
                }
            }

            // then some heuristic matches (since System.Mime.ContentType is a performance hit)
            // start by looking for a parameter. 

            // If none exists, we don't have an encoding
            int semiColonIndex = contentType.IndexOf(';');
            if (semiColonIndex == -1)
            {
                return null;
            }

            // optimize for charset being the first parameter
            int charsetValueIndex = -1;

            // for Indigo scenarios, we'll have "; charset=", so check for the c
            if ((contentType.Length > semiColonIndex + 11) // need room for parameter + charset + '=' 
                && contentType[semiColonIndex + 2] == 'c'
                && string.Compare("charset=", 0, contentType, semiColonIndex + 2, 8, StringComparison.OrdinalIgnoreCase) == 0)
            {
                charsetValueIndex = semiColonIndex + 10;
            }
            else
            {
                // look for charset= somewhere else in the message
                int paramIndex = contentType.IndexOf("charset=", semiColonIndex + 1, StringComparison.OrdinalIgnoreCase);
                if (paramIndex != -1)
                {
                    // validate there's only whitespace or semi-colons beforehand
                    for (int i = paramIndex - 1; i >= semiColonIndex; i--)
                    {
                        if (contentType[i] == ';')
                        {
                            charsetValueIndex = paramIndex + 8;
                            break;
                        }

                        if (contentType[i] == '\n')
                        {
                            if (i == semiColonIndex || contentType[i - 1] != '\r')
                            {
                                break;
                            }

                            i--;
                            continue;
                        }

                        if (contentType[i] != ' '
                            && contentType[i] != '\t')
                        {
                            break;
                        }
                    }
                }
            }

            string charSet;
            Encoding enc;

            // we have a possible charset value. If it's easy to parse, do so
            if (charsetValueIndex != -1)
            {
                // get the next semicolon
                semiColonIndex = contentType.IndexOf(';', charsetValueIndex);
                if (semiColonIndex == -1)
                {
                    charSet = contentType.Substring(charsetValueIndex);
                }
                else
                {
                    charSet = contentType.Substring(charsetValueIndex, semiColonIndex - charsetValueIndex);
                }

                // and some minimal quote stripping
                if (charSet.Length > 2 && charSet[0] == '"' && charSet[charSet.Length - 1] == '"')
                {
                    charSet = charSet.Substring(1, charSet.Length - 2);
                }

                if (TryGetEncodingFromCharSet(charSet, out enc))
                {
                    return enc;
                }
            }

            // our quick heuristics failed. fall back to System.Net
            try
            {
                MediaTypeHeaderValue parsedContentType = MediaTypeHeaderValue.Parse(contentType);
                charSet = parsedContentType.CharSet;
            }
            catch (FormatException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.EncoderBadContentType, e));
            }

            if (TryGetEncodingFromCharSet(charSet, out enc))
            {
                return enc;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.Format(SR.EncoderUnrecognizedCharSet, charSet)));
        }

        internal static string GetContentType(string mediaType, Encoding encoding)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}; charset={1}", mediaType, TextEncoderDefaults.EncodingToCharSet(encoding));
        }

        internal static Encoding[] GetSupportedEncodings()
        {
            Encoding[] supported = TextEncoderDefaults.SupportedEncodings;
            Encoding[] enc = new Encoding[supported.Length];
            Array.Copy(supported, enc, supported.Length);
            return enc;
        }

        private static bool TryGetEncodingFromCharSet(string charSet, out Encoding encoding)
        {
            encoding = null;
            if (charSet == null || charSet.Length == 0)
            {
                return true;
            }

            return TextEncoderDefaults.TryGetEncoding(charSet, out encoding);
        }
    }
}
