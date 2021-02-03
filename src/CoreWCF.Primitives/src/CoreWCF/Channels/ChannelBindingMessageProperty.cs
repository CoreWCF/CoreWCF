// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Authentication.ExtendedProtection;

namespace CoreWCF.Channels
{
    public sealed class ChannelBindingMessageProperty : IDisposable, IMessageProperty
    {
        internal const string PropertyName = "ChannelBindingMessageProperty";
        private readonly ChannelBinding _channelBinding;
        private readonly object _thisLock;
        private readonly bool _ownsCleanup;
        private int _refCount;

        public ChannelBindingMessageProperty(ChannelBinding channelBinding, bool ownsCleanup)
        {
            _channelBinding = channelBinding ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channelBinding));
            _refCount = 1;
            _thisLock = new object();
            _ownsCleanup = ownsCleanup;
        }

        public static string Name { get { return PropertyName; } }

        private bool IsDisposed
        {
            get
            {
                return _refCount <= 0;
            }
        }

        public ChannelBinding ChannelBinding
        {
            get
            {
                ThrowIfDisposed();
                return _channelBinding;
            }
        }

        public static bool TryGet(Message message, out ChannelBindingMessageProperty property)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            return TryGet(message.Properties, out property);
        }

        public static bool TryGet(MessageProperties properties, out ChannelBindingMessageProperty property)
        {
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(properties));
            }

            property = null;

            if (properties.TryGetValue(Name, out object value))
            {
                property = value as ChannelBindingMessageProperty;
                return property != null;
            }

            return false;
        }
        IMessageProperty IMessageProperty.CreateCopy()
        {
            lock (_thisLock)
            {
                ThrowIfDisposed();
                _refCount++;
                return this;
            }
        }

        void IDisposable.Dispose()
        {
            if (!IsDisposed)
            {
                lock (_thisLock)
                {
                    if (!IsDisposed && --_refCount == 0)
                    {
                        if (_ownsCleanup)
                        {
                            // Accessing via IDisposable to avoid Security check (functionally the same)
                            ((IDisposable)_channelBinding).Dispose();
                        }
                    }
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }
    }
}
