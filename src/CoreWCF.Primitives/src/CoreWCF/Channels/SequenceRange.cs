// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Runtime;
using System.Globalization;
using System;

namespace CoreWCF.Channels
{
    internal struct SequenceRange
    {

        // constructors
        public SequenceRange(long number)
            : this(number, number)
        {
        }

        public SequenceRange(long lower, long upper)
        {
            if (lower < 0)
            {
                throw Fx.AssertAndThrow("Argument lower cannot be negative.");
            }

            if (lower > upper)
            {
                throw Fx.AssertAndThrow("Argument upper cannot be less than argument lower.");
            }

            Lower = lower;
            Upper = upper;
        }

        // properties
        public long Lower { get; }

        public long Upper { get; }

        public static bool operator ==(SequenceRange a, SequenceRange b)
        {
            return (a.Lower == b.Lower) && (a.Upper == b.Upper);
        }

        public static bool operator !=(SequenceRange a, SequenceRange b)
        {
            return !(a == b);
        }

        public bool Contains(long number)
        {
            return number >= Lower && number <= Upper;
        }

        public bool Contains(SequenceRange range)
        {
            return range.Lower >= Lower && range.Upper <= Upper;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            else if (obj is SequenceRange)
            {
                return this == (SequenceRange)obj;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            long hashCode = Upper ^ (Upper - Lower);
            return (int)((hashCode << 32) ^ (hashCode >> 32));
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}", Lower, Upper);
        }
    }
}
