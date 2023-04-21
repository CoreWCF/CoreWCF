// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public enum DeadLetterQueue
    {
        None,
        System,
        Custom
    }

    internal static class DeadLetterQueueHelper
    {
        public static bool IsDefined(DeadLetterQueue mode)
        {
            return mode >= DeadLetterQueue.None && mode <= DeadLetterQueue.Custom;
        }
    }
}
