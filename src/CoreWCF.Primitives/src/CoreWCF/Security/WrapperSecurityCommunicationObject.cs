
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Diagnostics;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using System;
using System.Threading;

namespace CoreWCF.Security
{
    internal class WrapperSecurityCommunicationObject : CommunicationObject
    {
        private ISecurityCommunicationObject _innerCommunicationObject;

        public WrapperSecurityCommunicationObject(ISecurityCommunicationObject innerCommunicationObject)
            : base()
        {
            _innerCommunicationObject = innerCommunicationObject ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(innerCommunicationObject));
        }

        protected override Type GetCommunicationObjectType()
        {
            return _innerCommunicationObject.GetType();
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return _innerCommunicationObject.DefaultCloseTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return _innerCommunicationObject.DefaultOpenTimeout; }
        }

        protected override void OnAbort()
        {
            _innerCommunicationObject.OnAbort();
        }



        protected override void OnFaulted()
        {
            _innerCommunicationObject.OnFaulted();
            base.OnFaulted();
        }

        new internal void ThrowIfDisposedOrImmutable()
        {
            base.ThrowIfDisposedOrImmutable();
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return _innerCommunicationObject.OnCloseAsync(DefaultCloseTimeout);
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return _innerCommunicationObject.OnOpenAsync(DefaultOpenTimeout);
        }
    }

    //internal abstract class CommunicationObjectSecurityTokenProvider : SecurityTokenProvider, IAsyncCommunicationObject, ISecurityCommunicationObject
    //{
    //   // private EventTraceActivity _eventTraceActivity;

    //    protected CommunicationObjectSecurityTokenProvider()
    //    {
    //        CommunicationObject = new WrapperSecurityCommunicationObject(this);
    //    }

    //    //internal EventTraceActivity EventTraceActivity
    //    //{
    //    //    get
    //    //    {
    //    //        if (_eventTraceActivity == null)
    //    //        {
    //    //            _eventTraceActivity = EventTraceActivity.GetFromThreadOrCreate();
    //    //        }
    //    //        return _eventTraceActivity;
    //    //    }
    //    //}

    //    protected WrapperSecurityCommunicationObject CommunicationObject { get; }

    //    public event EventHandler Closed
    //    {
    //        add { CommunicationObject.Closed += value; }
    //        remove { CommunicationObject.Closed -= value; }
    //    }

    //    public event EventHandler Closing
    //    {
    //        add { CommunicationObject.Closing += value; }
    //        remove { CommunicationObject.Closing -= value; }
    //    }

    //    public event EventHandler Faulted
    //    {
    //        add { CommunicationObject.Faulted += value; }
    //        remove { CommunicationObject.Faulted -= value; }
    //    }

    //    public event EventHandler Opened
    //    {
    //        add { CommunicationObject.Opened += value; }
    //        remove { CommunicationObject.Opened -= value; }
    //    }

    //    public event EventHandler Opening
    //    {
    //        add { CommunicationObject.Opening += value; }
    //        remove { CommunicationObject.Opening -= value; }
    //    }

    //    public CommunicationState State
    //    {
    //        get { return CommunicationObject.State; }
    //    }

    //    public virtual TimeSpan DefaultOpenTimeout
    //    {
    //        get { return ServiceDefaults.OpenTimeout; }
    //    }

    //    public virtual TimeSpan DefaultCloseTimeout
    //    {
    //        get { return ServiceDefaults.CloseTimeout; }
    //    }

    //    // communication object
    //    public void Abort()
    //    {
    //        CommunicationObject.Abort();
    //    }

    //    public void Close()
    //    {
    //        CommunicationObject.Close();
    //    }

    //    public Task CloseAsync(TimeSpan timeout)
    //    {
    //        return ((IAsyncCommunicationObject)CommunicationObject).CloseAsync(timeout);
    //    }

    //    public void Close(TimeSpan timeout)
    //    {
    //        CommunicationObject.Close(timeout);
    //    }

    //    public IAsyncResult BeginClose(AsyncCallback callback, object state)
    //    {
    //        return CommunicationObject.BeginClose(callback, state);
    //    }

    //    public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
    //    {
    //        return CommunicationObject.BeginClose(timeout, callback, state);
    //    }

    //    public void EndClose(IAsyncResult result)
    //    {
    //        CommunicationObject.EndClose(result);
    //    }

    //    public void Open()
    //    {
    //        CommunicationObject.Open();
    //    }

    //    public Task OpenAsync(TimeSpan timeout)
    //    {
    //        return ((IAsyncCommunicationObject)CommunicationObject).OpenAsync(timeout);
    //    }

    //    public void Open(TimeSpan timeout)
    //    {
    //        CommunicationObject.Open(timeout);
    //    }

    //    public IAsyncResult BeginOpen(AsyncCallback callback, object state)
    //    {
    //        return CommunicationObject.BeginOpen(callback, state);
    //    }

    //    public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
    //    {
    //        return CommunicationObject.BeginOpen(timeout, callback, state);
    //    }

    //    public void EndOpen(IAsyncResult result)
    //    {
    //        CommunicationObject.EndOpen(result);
    //    }

    //    public void Dispose()
    //    {
    //        Close();
    //    }

    //    // ISecurityCommunicationObject methods
    //    public virtual void OnAbort()
    //    {
    //    }

    //    public virtual Task OnCloseAsync(TimeSpan timeout)
    //    {
    //        return Task.CompletedTask;
    //    }

    //    public virtual void OnClosed()
    //    {
    //    }

    //    public virtual void OnClosing()
    //    {
    //    }

    //    public virtual void OnFaulted()
    //    {
    //        OnAbort();
    //    }

    //    public virtual Task OnOpenAsync(TimeSpan timeout)
    //    {
    //        return Task.CompletedTask;
    //    }

    //    public virtual void OnOpened()
    //    {
    //    }

    //    public virtual void OnOpening()
    //    {
    //    }
    //}

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

        public CommunicationState State
        {
            get { return CommunicationObject.State; }
        }

        public virtual TimeSpan DefaultOpenTimeout
        {
            get { return ServiceDefaults.OpenTimeout; }
        }

        public virtual TimeSpan DefaultCloseTimeout
        {
            get { return ServiceDefaults.CloseTimeout; }
        }

        // communication object
        public void Abort()
        {
            CommunicationObject.Abort();
        }

       
        public virtual void OnClose(TimeSpan timeout)
        {
        }

        public Task OnCloseAsync(TimeSpan timeout)
        {
            return Task.CompletedTask;
        }

        public virtual void OnFaulted()
        {
            Abort();
        }

        public virtual void OnOpen(TimeSpan timeout)
        {
        }

        public Task OnOpenAsync(TimeSpan timeout)
        {
            return Task.CompletedTask;
        }

        public virtual Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        public virtual  Task CloseAsync(CancellationToken token)
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

        public void OnAbort()
        {
            
        }
    }
}
