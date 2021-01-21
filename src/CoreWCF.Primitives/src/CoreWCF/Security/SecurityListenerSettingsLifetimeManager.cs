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
        private readonly SecurityProtocolFactory securityProtocolFactory;
        private readonly SecuritySessionServerSettings sessionSettings;
        private readonly bool sessionMode;
        private int referenceCount;

        public SecurityListenerSettingsLifetimeManager(SecurityProtocolFactory securityProtocolFactory, SecuritySessionServerSettings sessionSettings, bool sessionMode)
        {
            this.securityProtocolFactory = securityProtocolFactory;
            this.sessionSettings = sessionSettings;
            this.sessionMode = sessionMode;
            referenceCount = 1;
        }

        public void Abort()
        {
            if (Interlocked.Decrement(ref referenceCount) == 0)
            {
                AbortCore();
            }
        }

        public void AddReference()
        {
            Interlocked.Increment(ref referenceCount);
        }

        public Task OpenAsync(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            if (sessionMode && sessionSettings != null)
            {
                sessionSettings.OpenAsync(timeoutHelper.RemainingTime());
            }

            /* if (this.securityProtocolFactory != null)
             {
                 this.securityProtocolFactory.OpenAsync(timeoutHelper.RemainingTime());
             }*/

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
            if (Interlocked.Decrement(ref referenceCount) == 0)
            {
                TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                bool throwing = true;
                try
                {
                    if (securityProtocolFactory != null)
                    {
                        securityProtocolFactory.OnCloseAsync(timeoutHelper.RemainingTime());
                    }
                    if (sessionMode && sessionSettings != null)
                    {
                        sessionSettings.CloseAsync(timeoutHelper.RemainingTime());
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
            if (securityProtocolFactory != null)
            {
                securityProtocolFactory.OnCloseAsync(TimeSpan.Zero);
            }
            if (sessionMode && sessionSettings != null)
            {
                sessionSettings.Abort();
            }
        }

    }
}
