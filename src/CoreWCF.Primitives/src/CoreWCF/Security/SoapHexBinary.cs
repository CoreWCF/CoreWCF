// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Text;

namespace CoreWCF.Security
{
    internal sealed class SoapHexBinary
    {
        private readonly StringBuilder _sb = new StringBuilder(100);

        public SoapHexBinary()
        {
        }

        public SoapHexBinary(byte[] value)
        {
            Value = value;
        }

        public byte[] Value { get; set; }

        public override string ToString()
        {
            _sb.Length = 0;
            for (int i = 0; i < Value.Length; i++)
            {
                string s = Value[i].ToString("X", CultureInfo.InvariantCulture);
                if (s.Length == 1)
                {
                    _sb.Append('0');
                }

                _sb.Append(s);
            }
            return _sb.ToString();
        }

        public static SoapHexBinary Parse(string value)
        {
            return new SoapHexBinary(ToByteArray(FilterBin64(value)));
        }

        private static byte[] ToByteArray(string value)
        {
            char[] cA = value.ToCharArray();
            if (cA.Length % 2 != 0)
            {
                throw new FormatException(SR.Format("Remoting_SOAPInteropxsdInvalid", "xsd:hexBinary", value));
            }
            byte[] bA = new byte[cA.Length / 2];
            for (int i = 0; i < cA.Length / 2; i++)
            {
                bA[i] = (byte)(ToByte(cA[i * 2], value) * 16 + ToByte(cA[i * 2 + 1], value));
            }

            return bA;
        }

        private static byte ToByte(char c, string value)
        {
            string s = c.ToString();
            byte b;
            try
            {
                s = c.ToString();
                b = byte.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw new FormatException(SR.Format("Remoting_SOAPInteropxsdInvalid", "xsd:hexBinary", value));
            }

            return b;
        }

        internal static string FilterBin64(string value)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (!(value[i] == ' ' || value[i] == '\n' || value[i] == '\r'))
                {
                    sb.Append(value[i]);
                }
            }
            return sb.ToString();
        }
    }
}
