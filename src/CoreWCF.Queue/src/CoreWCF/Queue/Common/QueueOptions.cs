// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;

namespace CoreWCF.Queue.Common
{
    public class QueueOptions
    {
        private int _concurrencyLevel = 1;

        public int ConcurrencyLevel
        {
            get { return _concurrencyLevel; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(ConcurrencyLevel), SR.ValueMustBeGreaterThanZero);
                _concurrencyLevel = value;
            }
        }
    }
}
