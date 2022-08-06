// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    internal static class DecoderHelper
    {
        public static void ValidateSize(int size)
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(SR.ValueMustBePositive);
            }
        }
    }
}
