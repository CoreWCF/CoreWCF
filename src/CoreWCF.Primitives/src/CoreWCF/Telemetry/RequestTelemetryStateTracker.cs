// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CoreWCF.Channels;

namespace CoreWCF.Telemetry;


internal static class RequestTelemetryStateTracker
{
    private static readonly Dictionary<string, Entry> OutstandingRequestStates = new() { };
    private static readonly SortedSet<EntryTimeoutProperties> TimeoutQueue = new() { };
    private static readonly object Sync = new();
    private static readonly Timer Timer = new(OnTimer);
    private static long currentTimerDueAt = Timeout.Infinite;

    public static void PushTelemetryState(Message request, RequestTelemetryState telemetryState, TimeSpan timeout, Action<RequestTelemetryState> timeoutCallback)
    {
        var messageId = request?.Headers.MessageId?.ToString();
        if (messageId != null)
        {
            var expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)timeout.TotalMilliseconds;
            var timeoutProps = new EntryTimeoutProperties(messageId, expiresAt, timeoutCallback);
            var entry = new Entry(telemetryState, timeoutProps);
            lock (Sync)
            {
                OutstandingRequestStates.Add(messageId, entry);
                TimeoutQueue.Add(timeoutProps);
                SetTimerEarlierIfNeeded(expiresAt);
            }
        }
    }

    public static RequestTelemetryState? PopTelemetryState(Message reply)
    {
        var relatesTo = reply?.Headers.RelatesTo?.ToString();
        return relatesTo == null ? null : PopTelemetryState(relatesTo);
    }

    private static RequestTelemetryState? PopTelemetryState(string messageId)
    {
        lock (Sync)
        {
            if (OutstandingRequestStates.TryGetValue(messageId, out var entry))
            {
                OutstandingRequestStates.Remove(messageId);
                TimeoutQueue.Remove(entry.TimeoutProperties);
            }

            return entry?.State;
        }
    }

    private static void OnTimer(object? state)
    {
        List<Tuple<EntryTimeoutProperties, Entry>>? timedOutEntries = null;
        lock (Sync)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            EntryTimeoutProperties? nextToExpire = null;
            foreach (var entryTimeoutProps in TimeoutQueue)
            {
                if (entryTimeoutProps.ExpiresAt <= now)
                {
                    timedOutEntries ??= new() { };

                    timedOutEntries.Add(new(entryTimeoutProps, OutstandingRequestStates[entryTimeoutProps.MessageId]));
                }
                else
                {
                    nextToExpire = entryTimeoutProps;
                    break;
                }
            }

            foreach (var entry in timedOutEntries ?? Enumerable.Empty<Tuple<EntryTimeoutProperties, Entry>>())
            {
                OutstandingRequestStates.Remove(entry.Item1.MessageId);
                TimeoutQueue.Remove(entry.Item1);
            }

            // when there's no more outstanding requests we set time timer to infinite, effectively disabling it until another request arrives
            var nextExpiry = nextToExpire?.ExpiresAt ?? Timeout.Infinite;
            SetTimer(nextExpiry);
        }

        timedOutEntries?.ForEach(entry => entry.Item1.TimeoutCallback(entry.Item2.State));
    }

    private static void SetTimerEarlierIfNeeded(long dueAt)
    {
        if (currentTimerDueAt == Timeout.Infinite || currentTimerDueAt > dueAt)
        {
            SetTimer(dueAt);
        }
    }

    private static void SetTimer(long dueAt)
    {
        long dueIn;
        if (dueAt == Timeout.Infinite)
        {
            dueIn = Timeout.Infinite;
        }
        else
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            dueIn = Math.Max(0, dueAt - now) + 50; // add 50ms here because the timer is not precise and often fires just a bit early
        }

        Timer.Change(dueIn, Timeout.Infinite);
        currentTimerDueAt = dueAt;
    }

    private class Entry
    {
        public readonly RequestTelemetryState State;
        public readonly EntryTimeoutProperties TimeoutProperties;

        public Entry(RequestTelemetryState state, EntryTimeoutProperties timeoutProperties)
        {
            this.State = state;
            this.TimeoutProperties = timeoutProperties;
        }
    }

    private class EntryTimeoutProperties : IComparable
    {
        public readonly string MessageId;
        public readonly long ExpiresAt;
        public readonly Action<RequestTelemetryState> TimeoutCallback;

        public EntryTimeoutProperties(string messageId, long expiresAt, Action<RequestTelemetryState> timeoutCallback)
        {
            this.MessageId = messageId;
            this.ExpiresAt = expiresAt;
            this.TimeoutCallback = timeoutCallback;
        }

        public int CompareTo(object? obj)
        {
            if (obj is not EntryTimeoutProperties other)
            {
                return 1;
            }

            var result = this.ExpiresAt.CompareTo(other.ExpiresAt);
            if (result == 0)
            {
                result = string.Compare(this.MessageId, other.MessageId, StringComparison.Ordinal);
            }

            return result;
        }
    }
}
