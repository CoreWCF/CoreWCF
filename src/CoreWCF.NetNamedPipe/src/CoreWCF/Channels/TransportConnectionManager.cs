// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class TransportConnectionManager
    {
        private long _lastConnectionId = long.MinValue;
        private readonly ConcurrentDictionary<long, ConnectionReference> _connectionReferences = new ConcurrentDictionary<long, ConnectionReference>();

        public long GetNewConnectionId() => Interlocked.Increment(ref _lastConnectionId);

        public void AddConnection(long id, NamedPipeConnection connection)
        {
            var connectionReference = new ConnectionReference(id, connection, this);

            if (!_connectionReferences.TryAdd(id, connectionReference))
            {
                throw new ArgumentException("Unable to add specified id.", nameof(id));
            }
        }

        public void RemoveConnection(long id)
        {
            if (!_connectionReferences.TryRemove(id, out _))
            {
                throw new ArgumentException("No value found for the specified id.", nameof(id));
            }
        }

        // This is only called by the ConnectionManager when the connection reference becomes
        // unrooted because the application never completed.
        public void StopTracking(long id)
        {
            if (!_connectionReferences.TryRemove(id, out _))
            {
                throw new ArgumentException("No value found for the specified id.", nameof(id));
            }
        }

        public async Task<bool> CloseAllConnectionsAsync(CancellationToken token)
        {
            var closeTasks = new List<Task>();

            foreach (var kvp in _connectionReferences)
            {
                if (kvp.Value.TryGetConnection(out var connection))
                {
                    connection.RequestClose();
                    closeTasks.Add(connection.ExecutionTask);
                }
            }

            var allClosedTask = Task.WhenAll(closeTasks.ToArray());
            return await Task.WhenAny(allClosedTask).CancellableAsyncWait(token) == allClosedTask;
        }
    }
}
