// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Security
{
    internal class SecurityListenerSettingsLifetimeManager
    {
        private readonly SecurityProtocolFactory _securityProtocolFactory;
        private readonly SecuritySessionServerSettings _sessionSettings;
        private readonly bool _sessionMode;
        private int _referenceCount;

        public SecurityListenerSettingsLifetimeManager(SecurityProtocolFactory securityProtocolFactory, SecuritySessionServerSettings sessionSettings, bool sessionMode)
        {
            _securityProtocolFactory = securityProtocolFactory;
            _sessionSettings = sessionSettings;
            _sessionMode = sessionMode;
            _referenceCount = 1;
        }

        public void Abort()
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                AbortCore();
            }
        }

        public void AddReference()
        {
            Interlocked.Increment(ref _referenceCount);
        }

        public async Task OpenAsync(CancellationToken token)
        {
            if (_sessionMode && _sessionSettings != null)
            {
                await _sessionSettings.OpenAsync(token);
            }

            if (_securityProtocolFactory != null && _securityProtocolFactory.CommunicationObject.State == CommunicationState.Created)
             {
                await _securityProtocolFactory.OpenAsync(token);
             }

            // this.SetBufferManager();
        }

        private void SetBufferManager()
        {
            /* TODO
                ITransportFactorySettings transportSettings = this.innerListener.GetProperty<ITransportFactorySettings>();
                if (transportSettings == null)
                    return;

                BufferManager bufferManager = transportSettings.BufferManager;
                if (bufferManager == null)
                    return;

                if (this.securityProtocolFactory != null)
                {
                    this.securityProtocolFactory.StreamBufferManager = bufferManager;
                }

                if (this.sessionMode && this.sessionSettings != null && this.sessionSettings.SessionProtocolFactory != null)
                {
                    this.sessionSettings.SessionProtocolFactory.StreamBufferManager = bufferManager;
                }
                */
        }

        public async Task CloseAsync(CancellationToken token)
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                bool throwing = true;
                try
                {
                    if (_securityProtocolFactory != null)
                    {
                        await _securityProtocolFactory.CloseAsync(false, token);
                    }

                    if (_sessionMode && _sessionSettings != null)
                    {
                        await _sessionSettings.CloseAsync(token);
                    }

                    throwing = false;
                }
                finally
                {
                    if (throwing)
                    {
                        AbortCore();
                    }
                }
            }
        }

        private void AbortCore()
        {
            if (_securityProtocolFactory != null)
            {
                _securityProtocolFactory.OnCloseAsync(default).GetAwaiter().GetResult();
            }
            if (_sessionMode && _sessionSettings != null)
            {
                _sessionSettings.Abort();
            }
        }
    }
}
