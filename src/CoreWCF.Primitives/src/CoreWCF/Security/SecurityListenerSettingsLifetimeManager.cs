using System;
using System.Collections.Generic;
using CoreWCF.Runtime;
using System.Runtime;
using CoreWCF;
using CoreWCF.Channels;
using System.Threading;

namespace CoreWCF.Security
{
    class SecurityListenerSettingsLifetimeManager
    {
        SecurityProtocolFactory securityProtocolFactory;
        SecuritySessionServerSettings sessionSettings;
        bool sessionMode;
       // IChannelListener innerListener;
        int referenceCount;

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
               // AbortCore();
            }
        }

        public void AddReference()
        {
            Interlocked.Increment(ref this.referenceCount);
        }

        public void Init(TimeSpan timeout) // renaming open to Init
        {
            //TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            //if (this.securityProtocolFactory != null)
            //{
            //    this.securityProtocolFactory.Init(timeoutHelper.RemainingTime());
            //}
            //if (this.sessionMode && this.sessionSettings != null)
            //{
            //    this.sessionSettings.Init(timeoutHelper.RemainingTime());
            //} 

         //   this.innerListener.Open(timeoutHelper.RemainingTime());

            this.SetBufferManager();        
        }

        void SetBufferManager()
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

    }
}
