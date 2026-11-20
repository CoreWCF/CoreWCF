// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    internal class WrapperSecurityCommunicationObject : CommunicationObject
    {
        private readonly ISecurityCommunicationObject _innerCommunicationObject;

        public WrapperSecurityCommunicationObject(ISecurityCommunicationObject innerCommunicationObject) : base()
        {
            _innerCommunicationObject = innerCommunicationObject ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerCommunicationObject));
        }

        protected override Type GetCommunicationObjectType()
        {
            return _innerCommunicationObject.GetType();
        }

        protected override TimeSpan DefaultCloseTimeout => _innerCommunicationObject.DefaultCloseTimeout;

        protected override TimeSpan DefaultOpenTimeout => _innerCommunicationObject.DefaultOpenTimeout;

        protected override void OnAbort()
        {
            _innerCommunicationObject.OnAbort();
        }

        protected override void OnFaulted()
        {
            _innerCommunicationObject.OnFaulted();
            base.OnFaulted();
        }

        internal new void ThrowIfDisposedOrImmutable()
        {
            base.ThrowIfDisposedOrImmutable();
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return _innerCommunicationObject.OnCloseAsync(TimeoutHelper.GetCancellationToken(DefaultCloseTimeout));
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return _innerCommunicationObject.OnOpenAsync(TimeoutHelper.GetCancellationToken(DefaultOpenTimeout));
        }
    }

    internal abstract class CommunicationObjectSecurityTokenAuthenticator : SecurityTokenAuthenticator, ICommunicationObject, ISecurityCommunicationObject
    {
        protected CommunicationObjectSecurityTokenAuthenticator()
        {
            CommunicationObject = new WrapperSecurityCommunicationObject(this);
        }

        protected WrapperSecurityCommunicationObject CommunicationObject { get; }

        public event EventHandler Closed
        {
            add { CommunicationObject.Closed += value; }
            remove { CommunicationObject.Closed -= value; }
        }

        public event EventHandler Closing
        {
            add { CommunicationObject.Closing += value; }
            remove { CommunicationObject.Closing -= value; }
        }

        public event EventHandler Faulted
        {
            add { CommunicationObject.Faulted += value; }
            remove { CommunicationObject.Faulted -= value; }
        }

        public event EventHandler Opened
        {
            add { CommunicationObject.Opened += value; }
            remove { CommunicationObject.Opened -= value; }
        }

        public event EventHandler Opening
        {
            add { CommunicationObject.Opening += value; }
            remove { CommunicationObject.Opening -= value; }
        }

        public CommunicationState State => CommunicationObject.State;

        public virtual TimeSpan DefaultOpenTimeout => ServiceDefaults.OpenTimeout;

        public virtual TimeSpan DefaultCloseTimeout => ServiceDefaults.CloseTimeout;

        // communication object
        public void Abort()
        {
            CommunicationObject.Abort();
        }

        public Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public virtual void OnFaulted()
        {
            Abort();
        }

        public Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public virtual Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        public virtual Task CloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public virtual Task OpenAsync()
        {
            return Task.CompletedTask;
        }

        public virtual Task OpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public virtual void OnAbort()
        {
        }

        public void OnClosed()
        {
            throw new NotImplementedException();
        }

        public void OnClosing()
        {
            throw new NotImplementedException();
        }

        public void OnOpened()
        {
            throw new NotImplementedException();
        }

        public void OnOpening()
        {
            throw new NotImplementedException();
        }
    }
}
