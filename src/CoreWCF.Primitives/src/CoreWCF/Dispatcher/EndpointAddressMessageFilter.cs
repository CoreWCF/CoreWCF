// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    using HeaderBit = CoreWCF.Dispatcher.EndpointAddressProcessor.HeaderBit;
    using QName = CoreWCF.Dispatcher.EndpointAddressProcessor.QName;

    internal class EndpointAddressMessageFilter : MessageFilter
    {
        private readonly EndpointAddressMessageFilterHelper _helper;
        private readonly UriComparer _comparer;

        public EndpointAddressMessageFilter(EndpointAddress address)
            : this(address, false)
        {
        }

        public EndpointAddressMessageFilter(EndpointAddress address, bool includeHostNameInComparison)
        {
            Address = address ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(address));
            IncludeHostNameInComparison = includeHostNameInComparison;
            _helper = new EndpointAddressMessageFilterHelper(Address);

            if (includeHostNameInComparison)
            {
                _comparer = HostUriComparer.Value;
            }
            else
            {
                if (address.Uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                    address.Uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    _comparer = new NoHostUriComparer
                    {
                        ComparePort = false,
                        CompareScheme = false
                    };
                }
                else
                {
                    _comparer = NoHostUriComparer.Value;
                }
            }
        }

        public EndpointAddress Address { get; }

        public bool IncludeHostNameInComparison { get; }

        protected internal override IMessageFilterTable<FilterData> CreateFilterTable<FilterData>()
        {
            return new EndpointAddressMessageFilterTable<FilterData>();
        }

        public override bool Match(MessageBuffer messageBuffer)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            Message msg = messageBuffer.CreateMessage();
            try
            {
                return Match(msg);
            }
            finally
            {
                msg.Close();
            }
        }

        public override bool Match(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            // To
            Uri to = message.Headers.To;
            Uri actingAs = Address.Uri;

            if (to == null || !_comparer.Equals(actingAs, to))
            {
                return false;
            }

            return _helper.Match(message);
        }

        internal Dictionary<string, HeaderBit[]> HeaderLookup
        {
            get { return _helper.HeaderLookup; }
        }

        internal bool ComparePort
        {
            set
            {
                _comparer.ComparePort = value;
            }
        }

        internal abstract class UriComparer : EqualityComparer<Uri>
        {
            protected UriComparer()
            {
            }

            protected abstract bool CompareHost { get; }

            internal bool ComparePort { get; set; } = true;

            internal bool CompareScheme { get; set; } = true;

            public override bool Equals(Uri u1, Uri u2)
            {
                return EndpointAddress.UriEquals(u1, u2, true /* ignoreCase */, CompareHost, ComparePort, CompareScheme);
            }

            public override int GetHashCode(Uri uri)
            {
                return EndpointAddress.UriGetHashCode(uri, CompareHost, ComparePort);
            }
        }

        internal sealed class HostUriComparer : UriComparer
        {
            internal static readonly UriComparer Value = new HostUriComparer();

            private HostUriComparer()
            {
            }

            protected override bool CompareHost
            {
                get
                {
                    return true;
                }
            }
        }

        internal sealed class NoHostUriComparer : UriComparer
        {
            internal static readonly UriComparer Value = new NoHostUriComparer();

            internal NoHostUriComparer()
            {
            }

            protected override bool CompareHost
            {
                get
                {
                    return false;
                }
            }
        }
    }

    internal class EndpointAddressMessageFilterHelper
    {
        private readonly EndpointAddress _address;
        private readonly WeakReference _processorPool;
        private int _size;
        private byte[] _mask;
        private Dictionary<QName, int> _qnameLookup;
        private Dictionary<string, HeaderBit[]> _headerLookup;

        internal EndpointAddressMessageFilterHelper(EndpointAddress address)
        {
            _address = address;

            if (_address.Headers.Count > 0)
            {
                CreateMask();
                _processorPool = new WeakReference(null);
            }
            else
            {
                _qnameLookup = null;
                _headerLookup = null;
                _size = 0;
                _mask = null;
            }
        }

        private void CreateMask()
        {
            int nextBit = 0;
            string key;
            QName qname;
            _qnameLookup = new Dictionary<QName, int>(EndpointAddressProcessor.QNameComparer);
            _headerLookup = new Dictionary<string, HeaderBit[]>();
            StringBuilder builder = null;

            for (int i = 0; i < _address.Headers.Count; ++i)
            {
                if (builder == null)
                {
                    builder = new StringBuilder();
                }
                else
                {
                    builder.Remove(0, builder.Length);
                }

                key = _address.Headers[i].GetComparableForm(builder);
                if (_headerLookup.TryGetValue(key, out HeaderBit[] bits))
                {
                    Array.Resize(ref bits, bits.Length + 1);
                    bits[bits.Length - 1] = new HeaderBit(nextBit++);
                    _headerLookup[key] = bits;
                }
                else
                {
                    _headerLookup.Add(key, new HeaderBit[] { new HeaderBit(nextBit++) });
                    AddressHeader parameter = _address.Headers[i];

                    qname.name = parameter.Name;
                    qname.ns = parameter.Namespace;
                    _qnameLookup[qname] = 1;
                }
            }

            if (nextBit == 0)
            {
                _size = 0;
            }
            else
            {
                _size = (nextBit - 1) / 8 + 1;
            }

            if (_size > 0)
            {
                _mask = new byte[_size];
                for (int i = 0; i < _size - 1; ++i)
                {
                    _mask[i] = 0xff;
                }

                if ((nextBit % 8) == 0)
                {
                    _mask[_size - 1] = 0xff;
                }
                else
                {
                    _mask[_size - 1] = (byte)((1 << (nextBit % 8)) - 1);
                }
            }
        }

        internal Dictionary<string, HeaderBit[]> HeaderLookup
        {
            get
            {
                if (_headerLookup == null)
                {
                    _headerLookup = new Dictionary<string, HeaderBit[]>();
                }

                return _headerLookup;
            }
        }

        private EndpointAddressProcessor CreateProcessor(int length)
        {
            if (_processorPool.Target != null)
            {
                lock (_processorPool)
                {
                    object o = _processorPool.Target;
                    if (o != null)
                    {
                        EndpointAddressProcessor p = (EndpointAddressProcessor)o;
                        _processorPool.Target = p.Next;
                        p.Next = null;
                        p.Clear(length);
                        return p;
                    }
                }
            }

            return new EndpointAddressProcessor(length);
        }

        internal bool Match(Message message)
        {
            if (_size == 0)
            {
                return true;
            }

            EndpointAddressProcessor context = CreateProcessor(_size);
            context.ProcessHeaders(message, _qnameLookup, _headerLookup);
            bool result = context.TestExact(_mask);
            ReleaseProcessor(context);
            return result;
        }

        private void ReleaseProcessor(EndpointAddressProcessor context)
        {
            lock (_processorPool)
            {
                context.Next = _processorPool.Target as EndpointAddressProcessor;
                _processorPool.Target = context;
            }
        }
    }
}