using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CoreWCF.Security
{
    internal sealed class SoapHexBinary
    {
        private StringBuilder _sb = new StringBuilder(100);

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

        public static SoapHexBinary Parse(String value)
        {
            return new SoapHexBinary(ToByteArray(FilterBin64(value)));
        }

        private static Byte[] ToByteArray(String value)
        {
            Char[] cA = value.ToCharArray();
            if (cA.Length % 2 != 0)
            {
                throw new FormatException(SR.Format("Remoting_SOAPInteropxsdInvalid", "xsd:hexBinary", value));
            }
            Byte[] bA = new Byte[cA.Length / 2];
            for (int i = 0; i < cA.Length / 2; i++)
            {
                bA[i] = (Byte)(ToByte(cA[i * 2], value) * 16 + ToByte(cA[i * 2 + 1], value));
            }

            return bA;
        }

        private static Byte ToByte(Char c, String value)
        {
            Byte b = (Byte)0;
            String s = c.ToString();
            try
            {
                s = c.ToString();
                b = Byte.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw new FormatException(SR.Format("Remoting_SOAPInteropxsdInvalid", "xsd:hexBinary", value));
            }

            return b;
        }

        internal static String FilterBin64(String value)
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
