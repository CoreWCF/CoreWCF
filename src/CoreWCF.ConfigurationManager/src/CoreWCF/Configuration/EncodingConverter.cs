// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Text;

namespace CoreWCF.Configuration
{
    internal class EncodingConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (typeof(string) == sourceType)
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (typeof(InstanceDescriptor) == destinationType)
            {
                return true;
            }
            return base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is string)
            {
                string encoding = (string)value;

                Encoding retval;
                if (String.Compare(encoding, "utf-8", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // special case for utf-8 to match with what we do in the default text encoding
                    retval = Encoding.UTF8;
                }
                else
                {
                    retval = Encoding.GetEncoding(encoding);
                }
                if (retval == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("value", SR.ConfigInvalidEncodingValue);
                }
                return retval;
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (typeof(string) == destinationType && value is Encoding)
            {
                Encoding encoding = (Encoding)value;
                return encoding.HeaderName;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
