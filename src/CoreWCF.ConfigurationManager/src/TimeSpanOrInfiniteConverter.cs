// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Globalization;

namespace CoreWCF.Configuration
{
    class TimeSpanOrInfiniteConverter : TimeSpanConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo cultureInfo, object value, Type type)
        {
            if (value == null)
            {
                // todo ximik87 fxtrace?
                // throw FxTrace.Exception.ArgumentNull("value");
            }

            if (!(value is TimeSpan))
            {
                // todo ximik87 fxtrace?
                //  throw FxTrace.Exception.Argument("value", InternalSR.IncompatibleArgumentType(typeof(TimeSpan), value.GetType()));
            }

            if ((TimeSpan)value == TimeSpan.MaxValue)
            {
                return "Infinite";
            }
            else
            {
                return base.ConvertTo(context, cultureInfo, value, type);
            }
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo cultureInfo, object data)
        {
            if (string.Equals((string)data, "infinite", StringComparison.OrdinalIgnoreCase))
            {
                return TimeSpan.MaxValue;
            }
            else
            {
                return base.ConvertFrom(context, cultureInfo, data);
            }
        }
    }
}
