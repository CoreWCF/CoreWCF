// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Queue.Tests.InMemoryQueue;

/// <summary>
/// A thread-safe queue that supports async waiting for items.
/// Replaces the polling-based ConcurrentQueue approach with proper
/// semaphore-based synchronization to eliminate test flakiness.
/// </summary>
public class InMemoryMessageQueue : IDisposable
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(0);

    public void Enqueue(string message)
    {
        _queue.Enqueue(message);
        _semaphore.Release();
    }

    public async Task<string> DequeueAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        if (!_queue.TryDequeue(out string message))
        {
            throw new InvalidOperationException("Queue was empty after semaphore signal.");
        }

        return message;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
