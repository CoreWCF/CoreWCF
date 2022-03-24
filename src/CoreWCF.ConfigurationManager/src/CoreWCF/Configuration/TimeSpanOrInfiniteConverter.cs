// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Globalization;

namespace CoreWCF.Configuration
{
    public class TimeSpanOrInfiniteConverter : TimeSpanConverter
    {
        public override object ConvertTo(ITypeDescriptorContext ctx, CultureInfo ci, object value, Type type)
        {
            if (!(value is TimeSpan timeSpan))
                throw new ArgumentException(SR.Format(SR.ConfigInvalidClassFactoryValue, value.GetType().Name,
                    type.Name));

            return timeSpan == TimeSpan.MaxValue ? "Infinite" : base.ConvertTo(ctx, ci, timeSpan, type);
        }

        public override object ConvertFrom(ITypeDescriptorContext ctx, CultureInfo ci, object data)
        {
            return (string)data == "Infinite" ? TimeSpan.MaxValue : base.ConvertFrom(ctx, ci, data);
        }
    }
}
