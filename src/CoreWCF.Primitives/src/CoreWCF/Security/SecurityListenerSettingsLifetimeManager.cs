using System;
using System.Collections.Generic;
using CoreWCF.Runtime;
using System.Runtime;
using CoreWCF;
using CoreWCF.Channels;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Security
{
    internal class SecurityListenerSettingsLifetimeManager
    {
        private SecurityProtocolFactory securityProtocolFactory;
        private SecuritySessionServerSettings sessionSettings;
        private bool sessionMode;

        // IChannelListener innerListener;
        private int referenceCount;

        public SecurityListenerSettingsLifetimeManager(SecurityProtocolFactory securityProtocolFactory, SecuritySessionServerSettings sessionSettings, bool sessionMode)
        {
            this.securityProtocolFactory = securityProtocolFactory;
            this.sessionSettings = sessionSettings;
            this.sessionMode = sessionMode;
         //   this.innerListener = innerListener;
            // have a reference right from the start so that the state can be aborted before open
            referenceCount = 1;
        }

        public void Abort()
        {
            if (Interlocked.Decrement(ref this.referenceCount) == 0)
            {
                AbortCore();
            }
        }

        public void AddReference()
        {
            Interlocked.Increment(ref this.referenceCount);
        }

        public Task OpenAsync(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            if (this.securityProtocolFactory != null)
            {
                this.securityProtocolFactory.OpenAsync(timeoutHelper.RemainingTime());
            }
            if (this.sessionMode && this.sessionSettings != null)
            {
                this.sessionSettings.OpenAsync(timeoutHelper.RemainingTime());
            }

          //  this.innerListener.Open(timeoutHelper.RemainingTime());
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
            if (Interlocked.Decrement(ref this.referenceCount) == 0)
            {
                TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                bool throwing = true;
                try
                {
                    if (this.securityProtocolFactory != null)
                    {
                        this.securityProtocolFactory.OnCloseAsync(timeoutHelper.RemainingTime());
                    }
                    if (sessionMode && sessionSettings != null)
                    {
                        this.sessionSettings.CloseAsync(timeoutHelper.RemainingTime());
                    }
                   // this.innerListener.Close(timeoutHelper.RemainingTime());
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
        void AbortCore()
        {
            if (this.securityProtocolFactory != null)
            {
                this.securityProtocolFactory.OnCloseAsync(TimeSpan.Zero);
            }
            if (sessionMode && this.sessionSettings != null)
            {
                this.sessionSettings.Abort();
            }
            //this.innerListener.Abort();
        }

    }
}
