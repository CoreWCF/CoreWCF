// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;

namespace CoreWCF.Channels
{
    public sealed class RemoteEndpointMessageProperty
    {
        private string _address;
        private int _port;
        private IPEndPoint _remoteEndPoint;
        private IRemoteEndpointProvider _remoteEndpointProvider;
        private InitializationState _state;

        public RemoteEndpointMessageProperty(string address, int port)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(address));
            }

            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("port",
                    SR.Format(SR.ValueMustBeInRange, IPEndPoint.MinPort, IPEndPoint.MaxPort));
            }

            _port = port;
            _address = address;
            _state = InitializationState.All;
        }

        internal RemoteEndpointMessageProperty(IRemoteEndpointProvider remoteEndpointProvider)
        {
            _remoteEndpointProvider = remoteEndpointProvider;
        }

        public RemoteEndpointMessageProperty(IPEndPoint remoteEndPoint)
        {
            _remoteEndPoint = remoteEndPoint;
        }

        public static string Name
        {
            get { return typeof(RemoteEndpointMessageProperty).FullName; }
        }

        public string Address
        {
            get
            {
                if ((_state & InitializationState.Address) != InitializationState.Address)
                {
                    lock (ThisLock)
                    {
                        if ((_state & InitializationState.Address) != InitializationState.Address)
                        {
                            Initialize(false);
                        }
                    }
                }
                return _address;
            }
        }

        public int Port
        {
            get
            {
                if ((_state & InitializationState.Port) != InitializationState.Port)
                {
                    lock (ThisLock)
                    {
                        if ((_state & InitializationState.Port) != InitializationState.Port)
                        {
                            Initialize(true);
                        }
                    }
                }
                return _port;
            }
        }

        private object ThisLock { get; } = new object();

        private void Initialize(bool getHostedPort)
        {
            if (_remoteEndPoint != null)
            {
                _address = _remoteEndPoint.Address.ToString();
                _port = _remoteEndPoint.Port;
                _state = InitializationState.All;
                _remoteEndPoint = null;
            }
            else
            {
                if ((_state & InitializationState.Address) != InitializationState.Address)
                {
                    _address = _remoteEndpointProvider.GetAddress();
                    _state |= InitializationState.Address;
                }

                if (getHostedPort)
                {
                    _port = _remoteEndpointProvider.GetPort();
                    _state |= InitializationState.Port;
                    _remoteEndpointProvider = null;
                }
            }
        }

        internal interface IRemoteEndpointProvider
        {
            string GetAddress();
            int GetPort();
        }

        [Flags]
        private enum InitializationState
        {
            None = 0,
            Address = 1,
            Port = 2,
            All = 3
        }
    }
}
