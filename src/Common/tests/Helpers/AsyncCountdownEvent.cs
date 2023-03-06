// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace Helpers;

// https://devblogs.microsoft.com/pfxteam/building-async-coordination-primitives-part-3-asynccountdownevent/
public class AsyncCountdownEvent
{
    private readonly AsyncManualResetEvent m_amre = new();
    private int m_count;

    public AsyncCountdownEvent(int initialCount)
    {
        if (initialCount <= 0) throw new ArgumentOutOfRangeException(nameof(initialCount));
        m_count = initialCount;
    }

    public void AddCount(int value)
    {
        m_count = Interlocked.Add(ref m_count, value);
    }

    public Task WaitAsync() { return m_amre.WaitAsync(); }
    public void Signal()
    {
        if (m_count <= 0)
            throw new InvalidOperationException();

        int newCount = Interlocked.Decrement(ref m_count);
        if (newCount == 0)
            m_amre.Set();
        else if (newCount < 0)
            throw new InvalidOperationException();
    }
}
