﻿using System;
using System.Net;

namespace CoreWCF.Channels
{
    public sealed class RemoteEndpointMessageProperty
    {
        string address;
        int port;
        IPEndPoint remoteEndPoint;
        IRemoteEndpointProvider remoteEndpointProvider;
        InitializationState state;
        object thisLock = new object();

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

            this.port = port;
            this.address = address;
            state = InitializationState.All;
        }

        internal RemoteEndpointMessageProperty(IRemoteEndpointProvider remoteEndpointProvider)
        {
            this.remoteEndpointProvider = remoteEndpointProvider;
        }

        public RemoteEndpointMessageProperty(IPEndPoint remoteEndPoint)
        {
            this.remoteEndPoint = remoteEndPoint;
        }

        public static string Name
        {
            get { return typeof(RemoteEndpointMessageProperty).FullName; }
        }

        public string Address
        {
            get
            {
                if ((state & InitializationState.Address) != InitializationState.Address)
                {
                    lock (ThisLock)
                    {
                        if ((state & InitializationState.Address) != InitializationState.Address)
                        {
                            Initialize(false);
                        }
                    }
                }
                return address;
            }
        }

        public int Port
        {
            get
            {
                if ((state & InitializationState.Port) != InitializationState.Port)
                {
                    lock (ThisLock)
                    {
                        if ((state & InitializationState.Port) != InitializationState.Port)
                        {
                            Initialize(true);
                        }
                    }
                }
                return port;
            }
        }

        object ThisLock
        {
            get { return thisLock; }
        }

        void Initialize(bool getHostedPort)
        {
            if (remoteEndPoint != null)
            {
                address = remoteEndPoint.Address.ToString();
                port = remoteEndPoint.Port;
                state = InitializationState.All;
                remoteEndPoint = null;
            }
            else
            {
                if ((state & InitializationState.Address) != InitializationState.Address)
                {
                    address = remoteEndpointProvider.GetAddress();
                    state |= InitializationState.Address;
                }

                if (getHostedPort)
                {
                    port = remoteEndpointProvider.GetPort();
                    state |= InitializationState.Port;
                    remoteEndpointProvider = null;
                }
            }
        }

        internal interface IRemoteEndpointProvider
        {
            string GetAddress();
            int GetPort();
        }

        [Flags]
        enum InitializationState
        {
            None = 0,
            Address = 1,
            Port = 2,
            All = 3
        }
    }
}
