// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Dispatcher
{
    public sealed class ClientRuntime
    {
        //bool addTransactionFlowProperties = true;
        internal SynchronizedCollection<IClientMessageInspector> messageInspectors;
        internal SynchronizedKeyedCollection<string, ClientOperation> operations;
        private Type callbackProxyType;
        private readonly ProxyBehaviorCollection<IChannelInitializer> channelInitializers;
        private readonly string contractName;
        private readonly string contractNamespace;
        private Type contractProxyType;
        private DispatchRuntime dispatchRuntime;
        private IdentityVerifier identityVerifier;
        private IClientOperationSelector operationSelector;
        private ImmutableClientRuntime runtime;
        private readonly ClientOperation unhandled;
        private bool useSynchronizationContext = true;
        private Uri via;
        private readonly SharedRuntimeState shared;
        private int maxFaultSize;
        private bool messageVersionNoneFaultsEnabled;

        internal ClientRuntime(DispatchRuntime dispatchRuntime, SharedRuntimeState shared)
            : this(dispatchRuntime.EndpointDispatcher.ContractName,
                   dispatchRuntime.EndpointDispatcher.ContractNamespace,
                   shared)
        {
            if (dispatchRuntime == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dispatchRuntime));
            }

            this.dispatchRuntime = dispatchRuntime;
            this.shared = shared;

            Fx.Assert(shared.IsOnServer, "Server constructor called on client?");
        }

        internal ClientRuntime(string contractName, string contractNamespace)
            : this(contractName, contractNamespace, new SharedRuntimeState(false))
        {
            Fx.Assert(!shared.IsOnServer, "Client constructor called on server?");
        }

        private ClientRuntime(string contractName, string contractNamespace, SharedRuntimeState shared)
        {
            this.contractName = contractName;
            this.contractNamespace = contractNamespace;
            this.shared = shared;

            OperationCollection operations = new OperationCollection(this);
            this.operations = operations;
            channelInitializers = new ProxyBehaviorCollection<IChannelInitializer>(this);
            messageInspectors = new ProxyBehaviorCollection<IClientMessageInspector>(this);

            unhandled = new ClientOperation(this, "*", MessageHeaders.WildcardAction, MessageHeaders.WildcardAction);
            unhandled.InternalFormatter = new MessageOperationFormatter();
            maxFaultSize = TransportDefaults.MaxFaultSize;
        }

        //internal bool AddTransactionFlowProperties
        //{
        //    get { return this.addTransactionFlowProperties; }
        //    set
        //    {
        //        lock (this.ThisLock)
        //        {
        //            this.InvalidateRuntime();
        //            this.addTransactionFlowProperties = value;
        //        }
        //    }
        //}

        public Type CallbackClientType
        {
            get { return callbackProxyType; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    callbackProxyType = value;
                }
            }
        }

        public SynchronizedCollection<IChannelInitializer> ChannelInitializers
        {
            get { return channelInitializers; }
        }

        public string ContractName
        {
            get { return contractName; }
        }

        public string ContractNamespace
        {
            get { return contractNamespace; }
        }

        public Type ContractClientType
        {
            get { return contractProxyType; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    contractProxyType = value;
                }
            }
        }

        internal IdentityVerifier IdentityVerifier
        {
            get
            {
                if (identityVerifier == null)
                {
                    identityVerifier = IdentityVerifier.CreateDefault();
                }

                return identityVerifier;
            }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                InvalidateRuntime();

                identityVerifier = value;
            }
        }

        public Uri Via
        {
            get { return via; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    via = value;
                }
            }
        }

        public bool ValidateMustUnderstand
        {
            get { return shared.ValidateMustUnderstand; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    shared.ValidateMustUnderstand = value;
                }
            }
        }

        public bool MessageVersionNoneFaultsEnabled
        {
            get
            {
                return messageVersionNoneFaultsEnabled;
            }
            set
            {
                InvalidateRuntime();
                messageVersionNoneFaultsEnabled = value;
            }
        }

        internal DispatchRuntime DispatchRuntime
        {
            get { return dispatchRuntime; }
        }

        public DispatchRuntime CallbackDispatchRuntime
        {
            get
            {
                if (dispatchRuntime == null)
                {
                    dispatchRuntime = new DispatchRuntime(this, shared);
                }

                return dispatchRuntime;
            }
        }

        internal bool EnableFaults
        {
            get
            {
                if (IsOnServer)
                {
                    return dispatchRuntime.EnableFaults;
                }
                else
                {
                    return shared.EnableFaults;
                }
            }
            set
            {
                lock (ThisLock)
                {
                    if (IsOnServer)
                    {
                        string text = SR.SFxSetEnableFaultsOnChannelDispatcher0;
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(text));
                    }
                    else
                    {
                        InvalidateRuntime();
                        shared.EnableFaults = value;
                    }
                }
            }
        }

        public int MaxFaultSize
        {
            get
            {
                return maxFaultSize;
            }
            set
            {
                InvalidateRuntime();
                maxFaultSize = value;
            }
        }

        internal bool IsOnServer
        {
            get { return shared.IsOnServer; }
        }

        public bool ManualAddressing
        {
            get
            {
                if (IsOnServer)
                {
                    return dispatchRuntime.ManualAddressing;
                }
                else
                {
                    return shared.ManualAddressing;
                }
            }
            set
            {
                lock (ThisLock)
                {
                    if (IsOnServer)
                    {
                        string text = SR.SFxSetManualAddressingOnChannelDispatcher0;
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(text));
                    }
                    else
                    {
                        InvalidateRuntime();
                        shared.ManualAddressing = value;
                    }
                }
            }
        }

        internal int MaxParameterInspectors
        {
            get
            {
                lock (ThisLock)
                {
                    int max = 0;

                    for (int i = 0; i < operations.Count; i++)
                    {
                        max = System.Math.Max(max, operations[i].ParameterInspectors.Count);
                    }

                    return max;
                }
            }
        }

        internal ICollection<IClientMessageInspector> ClientMessageInspectors
        {
            get { return MessageInspectors; }
        }

        public SynchronizedCollection<IClientMessageInspector> MessageInspectors
        {
            get { return messageInspectors; }
        }

        public ICollection<ClientOperation> ClientOperations
        {
            get { return Operations; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public SynchronizedKeyedCollection<string, ClientOperation> Operations
        {
            get { return operations; }
        }

        internal IClientOperationSelector OperationSelector
        {
            get { return operationSelector; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    operationSelector = value;
                }
            }
        }

        internal object ThisLock
        {
            get { return shared; }
        }

        public ClientOperation UnhandledClientOperation
        {
            get { return unhandled; }
        }

        internal bool UseSynchronizationContext
        {
            get { return useSynchronizationContext; }
            set
            {
                lock (ThisLock)
                {
                    InvalidateRuntime();
                    useSynchronizationContext = value;
                }
            }
        }

        internal T[] GetArray<T>(SynchronizedCollection<T> collection)
        {
            lock (collection.SyncRoot)
            {
                if (collection.Count == 0)
                {
                    return Array.Empty<T>();
                }
                else
                {
                    T[] array = new T[collection.Count];
                    collection.CopyTo(array, 0);
                    return array;
                }
            }
        }

        internal ImmutableClientRuntime GetRuntime()
        {
            lock (ThisLock)
            {
                if (runtime == null)
                {
                    runtime = new ImmutableClientRuntime(this);
                }

                return runtime;
            }
        }

        internal void InvalidateRuntime()
        {
            lock (ThisLock)
            {
                shared.ThrowIfImmutable();
                runtime = null;
            }
        }

        internal void LockDownProperties()
        {
            shared.LockDownProperties();
        }

        internal SynchronizedCollection<T> NewBehaviorCollection<T>()
        {
            return new ProxyBehaviorCollection<T>(this);
        }

        internal bool IsFault(ref Message reply)
        {
            if (reply == null)
            {
                return false;
            }
            if (reply.IsFault)
            {
                return true;
            }
            if (MessageVersionNoneFaultsEnabled && IsMessageVersionNoneFault(ref reply, MaxFaultSize))
            {
                return true;
            }

            return false;
        }

        internal static bool IsMessageVersionNoneFault(ref Message message, int maxFaultSize)
        {
            if (message.Version != MessageVersion.None || message.IsEmpty)
            {
                return false;
            }
            //HttpResponseMessageProperty prop = message.Properties[HttpResponseMessageProperty.Name] as HttpResponseMessageProperty;
            //if (prop == null || prop.StatusCode != HttpStatusCode.InternalServerError)
            //{
            //    return false;
            //}
            using (MessageBuffer buffer = message.CreateBufferedCopy(maxFaultSize))
            {
                message.Close();
                message = buffer.CreateMessage();
                using (Message copy = buffer.CreateMessage())
                {
                    using (XmlDictionaryReader reader = copy.GetReaderAtBodyContents())
                    {
                        return reader.IsStartElement(XD.MessageDictionary.Fault, MessageVersion.None.Envelope.DictionaryNamespace);
                    }
                }
            }
        }

        private class ProxyBehaviorCollection<T> : SynchronizedCollection<T>
        {
            private readonly ClientRuntime outer;

            internal ProxyBehaviorCollection(ClientRuntime outer)
                : base(outer.ThisLock)
            {
                this.outer = outer;
            }

            protected override void ClearItems()
            {
                outer.InvalidateRuntime();
                base.ClearItems();
            }

            protected override void InsertItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                outer.InvalidateRuntime();
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                outer.InvalidateRuntime();
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                outer.InvalidateRuntime();
                base.SetItem(index, item);
            }
        }

        private class OperationCollection : SynchronizedKeyedCollection<string, ClientOperation>
        {
            private readonly ClientRuntime outer;

            internal OperationCollection(ClientRuntime outer)
                : base(outer.ThisLock)
            {
                this.outer = outer;
            }

            protected override void ClearItems()
            {
                outer.InvalidateRuntime();
                base.ClearItems();
            }

            protected override string GetKeyForItem(ClientOperation item)
            {
                return item.Name;
            }

            protected override void InsertItem(int index, ClientOperation item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                if (item.Parent != outer)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxMismatchedOperationParent);
                }

                outer.InvalidateRuntime();
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                outer.InvalidateRuntime();
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, ClientOperation item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                if (item.Parent != outer)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxMismatchedOperationParent);
                }

                outer.InvalidateRuntime();
                base.SetItem(index, item);
            }

            internal void InternalClearItems() { ClearItems(); }
            internal string InternalGetKeyForItem(ClientOperation item) { return GetKeyForItem(item); }
            internal void InternalInsertItem(int index, ClientOperation item) { InsertItem(index, item); }
            internal void InternalRemoveItem(int index) { RemoveItem(index); }
            internal void InternalSetItem(int index, ClientOperation item) { SetItem(index, item); }
        }

        private class OperationCollectionWrapper : KeyedCollection<string, ClientOperation>
        {
            private readonly OperationCollection inner;
            internal OperationCollectionWrapper(OperationCollection inner) { this.inner = inner; }
            protected override void ClearItems() { inner.InternalClearItems(); }
            protected override string GetKeyForItem(ClientOperation item) { return inner.InternalGetKeyForItem(item); }
            protected override void InsertItem(int index, ClientOperation item) { inner.InternalInsertItem(index, item); }
            protected override void RemoveItem(int index) { inner.InternalRemoveItem(index); }
            protected override void SetItem(int index, ClientOperation item) { inner.InternalSetItem(index, item); }
        }

    }

}