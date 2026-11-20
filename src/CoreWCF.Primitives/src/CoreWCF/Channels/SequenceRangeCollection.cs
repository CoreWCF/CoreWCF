// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using System;

namespace CoreWCF.Channels
{
    internal abstract class SequenceRangeCollection
    {
        private static readonly EmptyRangeCollection s_empty = new EmptyRangeCollection();
        private static readonly LowerComparer s_lowerComparer = new LowerComparer();
        private static readonly UpperComparer s_upperComparer = new UpperComparer();

        public static SequenceRangeCollection Empty => s_empty;

        public abstract SequenceRange this[int index] { get; }
        public abstract int Count { get; }
        public abstract bool Contains(long number);
        public abstract SequenceRangeCollection MergeWith(long number);
        public abstract SequenceRangeCollection MergeWith(SequenceRange range);

        private static SequenceRangeCollection GeneralCreate(SequenceRange[] sortedRanges)
        {
            if (sortedRanges.Length == 0)
            {
                return s_empty;
            }
            else if (sortedRanges.Length == 1)
            {
                return new SingleItemRangeCollection(sortedRanges[0]);
            }
            else
            {
                return new MultiItemRangeCollection(sortedRanges);
            }
        }

        private static SequenceRangeCollection GeneralMerge(SequenceRange[] sortedRanges, SequenceRange range)
        {
            if (sortedRanges.Length == 0)
            {
                return new SingleItemRangeCollection(range);
            }

            int lowerBound;

            if (sortedRanges.Length == 1)
            {
                // Avoid performance hit of binary search in single range case
                if (range.Lower == sortedRanges[0].Upper)
                {
                    lowerBound = 0;
                }
                else if (range.Lower < sortedRanges[0].Upper)
                {
                    lowerBound = ~0;
                }
                else
                {
                    lowerBound = ~1;
                }
            }
            else
            {
                lowerBound = Array.BinarySearch(sortedRanges, new SequenceRange(range.Lower), s_upperComparer);
            }

            if (lowerBound < 0)
            {
                lowerBound = ~lowerBound;

                if ((lowerBound > 0) && (sortedRanges[lowerBound - 1].Upper == range.Lower - 1))
                {
                    lowerBound--;
                }

                if (lowerBound == sortedRanges.Length)
                {
                    SequenceRange[] returnedRanges = new SequenceRange[sortedRanges.Length + 1];
                    Array.Copy(sortedRanges, returnedRanges, sortedRanges.Length);
                    returnedRanges[sortedRanges.Length] = range;
                    return GeneralCreate(returnedRanges);
                }
            }

            int upperBound;

            if (sortedRanges.Length == 1)
            {
                // Avoid performance hit of binary search in single range case
                if (range.Upper == sortedRanges[0].Lower)
                {
                    upperBound = 0;
                }
                else if (range.Upper < sortedRanges[0].Lower)
                {
                    upperBound = ~0;
                }
                else
                {
                    upperBound = ~1;
                }
            }
            else
            {
                upperBound = Array.BinarySearch(sortedRanges, new SequenceRange(range.Upper), s_lowerComparer);
            }

            if (upperBound < 0)
            {
                upperBound = ~upperBound;

                if (upperBound > 0)
                {
                    if ((upperBound == sortedRanges.Length) || (sortedRanges[upperBound].Lower != range.Upper + 1))
                    {
                        upperBound--;
                    }
                }
                else if (sortedRanges[0].Lower > range.Upper + 1)
                {
                    SequenceRange[] returnedRanges = new SequenceRange[sortedRanges.Length + 1];
                    Array.Copy(sortedRanges, 0, returnedRanges, 1, sortedRanges.Length);
                    returnedRanges[0] = range;
                    return GeneralCreate(returnedRanges);
                }
            }

            long newLower = (range.Lower < sortedRanges[lowerBound].Lower) ? range.Lower : sortedRanges[lowerBound].Lower;
            long newUpper = (range.Upper > sortedRanges[upperBound].Upper) ? range.Upper : sortedRanges[upperBound].Upper;

            int rangesRemoved = upperBound - lowerBound + 1;
            int rangesRemaining = sortedRanges.Length - rangesRemoved + 1;
            if (rangesRemaining == 1)
            {
                return new SingleItemRangeCollection(newLower, newUpper);
            }
            else
            {
                SequenceRange[] returnedRanges = new SequenceRange[rangesRemaining];
                Array.Copy(sortedRanges, returnedRanges, lowerBound);
                returnedRanges[lowerBound] = new SequenceRange(newLower, newUpper);
                Array.Copy(sortedRanges, upperBound + 1, returnedRanges, lowerBound + 1, sortedRanges.Length - upperBound - 1);
                return GeneralCreate(returnedRanges);
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Count; i++)
            {
                SequenceRange range = this[i];
                if (i > 0)
                {
                    builder.Append(',');
                }
                builder.Append(range.Lower);
                builder.Append('-');
                builder.Append(range.Upper);
            }
            return builder.ToString();
        }

        private class EmptyRangeCollection : SequenceRangeCollection
        {
            public override SequenceRange this[int index] => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index)));
            public override int Count => 0;
            public override bool Contains(long number) => false;
            public override SequenceRangeCollection MergeWith(long number) => new SingleItemRangeCollection(number, number);
            public override SequenceRangeCollection MergeWith(SequenceRange range) => new SingleItemRangeCollection(range);
        }

        private class MultiItemRangeCollection : SequenceRangeCollection
        {
            private readonly SequenceRange[] _ranges;

            public MultiItemRangeCollection(SequenceRange[] sortedRanges)
            {
                _ranges = sortedRanges;
            }

            public override SequenceRange this[int index]
            {
                get
                {
                    if (index < 0 || index >= _ranges.Length)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), index,
                                                    SR.Format(SR.ValueMustBeInRange, 0, _ranges.Length - 1)));
                    return _ranges[index];
                }
            }

            public override int Count => _ranges.Length;

            public override bool Contains(long number)
            {
                if (_ranges.Length == 0)
                {
                    return false;
                }
                else if (_ranges.Length == 1)
                {
                    return _ranges[0].Contains(number);
                }

                SequenceRange searchFor = new SequenceRange(number);
                int searchValue = Array.BinarySearch(_ranges, searchFor, s_lowerComparer);

                if (searchValue >= 0)
                {
                    return true;
                }

                searchValue = ~searchValue;

                if (searchValue == 0)
                {
                    return false;
                }

                return (_ranges[searchValue - 1].Upper >= number);
            }

            public override SequenceRangeCollection MergeWith(long number) => MergeWith(new SequenceRange(number));
            public override SequenceRangeCollection MergeWith(SequenceRange newRange) => GeneralMerge(_ranges, newRange);
        }

        private class SingleItemRangeCollection : SequenceRangeCollection
        {
            private readonly SequenceRange _range;

            public SingleItemRangeCollection(SequenceRange range)
            {
                this._range = range;
            }

            public SingleItemRangeCollection(long lower, long upper)
            {
                _range = new SequenceRange(lower, upper);
            }

            public override SequenceRange this[int index]
            {
                get
                {
                    if (index != 0)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index)));
                    return _range;
                }
            }

            public override int Count => 1;

            public override bool Contains(long number) => _range.Contains(number);

            public override SequenceRangeCollection MergeWith(long number)
            {
                if (number == _range.Upper + 1)
                {
                    return new SingleItemRangeCollection(_range.Lower, number);
                }
                else
                {
                    return MergeWith(new SequenceRange(number));
                }
            }

            public override SequenceRangeCollection MergeWith(SequenceRange newRange)
            {
                if (newRange.Lower == _range.Upper + 1)
                {
                    return new SingleItemRangeCollection(_range.Lower, newRange.Upper);
                }
                else if (_range.Contains(newRange))
                {
                    return this;
                }
                else if (newRange.Contains(_range))
                {
                    return new SingleItemRangeCollection(newRange);
                }
                else if (newRange.Upper == _range.Lower - 1)
                {
                    return new SingleItemRangeCollection(newRange.Lower, _range.Upper);
                }
                else
                {
                    return GeneralMerge(new SequenceRange[] { _range }, newRange);
                }
            }
        }

        private class LowerComparer : IComparer<SequenceRange>
        {
            public int Compare(SequenceRange x, SequenceRange y)
            {
                if (x.Lower < y.Lower)
                {
                    return -1;
                }
                else if (x.Lower > y.Lower)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        private class UpperComparer : IComparer<SequenceRange>
        {
            public int Compare(SequenceRange x, SequenceRange y)
            {
                if (x.Upper < y.Upper)
                {
                    return -1;
                }
                else if (x.Upper > y.Upper)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}
