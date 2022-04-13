// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

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

        public Task OpenAsync(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            if (_sessionMode && _sessionSettings != null)
            {
                _sessionSettings.OpenAsync(timeoutHelper.RemainingTime());
            }

            if (_securityProtocolFactory != null && _securityProtocolFactory.CommunicationObject.State == CommunicationState.Created)
             {
                _securityProtocolFactory.OpenAsync(timeoutHelper.RemainingTime());
             }

            // this.SetBufferManager();
            return Task.CompletedTask;
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

        public Task CloseAsync(TimeSpan timeout)
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                bool throwing = true;
                try
                {
                    if (_securityProtocolFactory != null)
                    {
                        _securityProtocolFactory.OnCloseAsync(timeoutHelper.RemainingTime());
                    }
                    if (_sessionMode && _sessionSettings != null)
                    {
                        _sessionSettings.CloseAsync(timeoutHelper.RemainingTime());
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
            return Task.CompletedTask;
        }

        private void AbortCore()
        {
            if (_securityProtocolFactory != null)
            {
                _securityProtocolFactory.OnCloseAsync(TimeSpan.Zero);
            }
            if (_sessionMode && _sessionSettings != null)
            {
                _sessionSettings.Abort();
            }
        }
    }
}
