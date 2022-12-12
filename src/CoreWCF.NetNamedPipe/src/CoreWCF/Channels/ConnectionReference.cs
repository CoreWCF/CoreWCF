// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    internal sealed class ConnectionReference
    {
        private readonly long _id;
        private readonly WeakReference<NamedPipeConnection> _weakReference;
        private readonly TransportConnectionManager _transportConnectionManager;

        public ConnectionReference(long id, NamedPipeConnection connection, TransportConnectionManager transportConnectionManager)
        {
            _id = id;

            _weakReference = new WeakReference<NamedPipeConnection>(connection);
            ConnectionId = connection.ConnectionContext.ConnectionId;

            _transportConnectionManager = transportConnectionManager;
        }

        public string ConnectionId { get; }

        public bool TryGetConnection(out NamedPipeConnection? connection)
        {
            return _weakReference.TryGetTarget(out connection);
        }

        public void StopTranssportTracking()
        {
            _transportConnectionManager.StopTracking(_id);
        }
    }
}
