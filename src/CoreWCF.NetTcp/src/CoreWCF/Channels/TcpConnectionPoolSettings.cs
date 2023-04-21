// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public sealed class TcpConnectionPoolSettings : ConnectionPoolSettings
    {
        //string _groupName;
        //TimeSpan _leaseTimeout;

        internal TcpConnectionPoolSettings()
        {
            //_groupName = ConnectionOrientedTransportDefaults.ConnectionPoolGroupName;
            //_leaseTimeout = TcpTransportDefaults.ConnectionLeaseTimeout;
        }

        internal TcpConnectionPoolSettings(TcpConnectionPoolSettings tcp) : base(tcp)
        {
            //_groupName = tcp._groupName;
            //_leaseTimeout = tcp._leaseTimeout;
        }

        internal TcpConnectionPoolSettings Clone()
        {
            return new TcpConnectionPoolSettings(this);
        }
    }
}
