// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;

namespace CoreWCF.Configuration
{
    class TimeSpanOrInfiniteValidator : TimeSpanValidator
    {
        public TimeSpanOrInfiniteValidator(TimeSpan minValue, TimeSpan maxValue)
            : base(minValue, maxValue)
        {
        }

        public override void Validate(object value)
        {
            if (value.GetType() == typeof(TimeSpan) && (TimeSpan)value == TimeSpan.MaxValue)
            {
                return; // we're good
            }

            base.Validate(value);
        }
    }
}
