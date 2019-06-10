﻿using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    using QName = CoreWCF.Dispatcher.EndpointAddressProcessor.QName;
    using HeaderBit = CoreWCF.Dispatcher.EndpointAddressProcessor.HeaderBit;

    internal class EndpointAddressMessageFilter : MessageFilter
    {
        EndpointAddress address;
        bool includeHostNameInComparison;
        EndpointAddressMessageFilterHelper helper;
        UriComparer comparer;

        public EndpointAddressMessageFilter(EndpointAddress address)
            : this(address, false)
        {
        }

        public EndpointAddressMessageFilter(EndpointAddress address, bool includeHostNameInComparison)
        {
            if (address == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(address));
            }

            this.address = address;
            this.includeHostNameInComparison = includeHostNameInComparison;
            helper = new EndpointAddressMessageFilterHelper(this.address);

            if (includeHostNameInComparison)
            {
                comparer = HostUriComparer.Value;
            }
            else
            {
                comparer = NoHostUriComparer.Value;
            }
        }

        public EndpointAddress Address
        {
            get
            {
                return address;
            }
        }

        public bool IncludeHostNameInComparison
        {
            get { return includeHostNameInComparison; }
        }

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
            Uri actingAs = address.Uri;

            if (to == null || !comparer.Equals(actingAs, to))
            {
                return false;
            }

            return helper.Match(message);
        }

        internal Dictionary<string, HeaderBit[]> HeaderLookup
        {
            get { return helper.HeaderLookup; }
        }

        internal bool ComparePort
        {
            set
            {
                comparer.ComparePort = value;
            }
        }

        internal abstract class UriComparer : EqualityComparer<Uri>
        {
            protected UriComparer()
            {
                ComparePort = true;
            }

            protected abstract bool CompareHost { get; }

            internal bool ComparePort
            {
                get;
                set;
            }

            public override bool Equals(Uri u1, Uri u2)
            {
                return EndpointAddress.UriEquals(u1, u2, true /* ignoreCase */, CompareHost, ComparePort);
            }

            public override int GetHashCode(Uri uri)
            {
                return EndpointAddress.UriGetHashCode(uri, CompareHost, ComparePort);
            }
        }

        internal sealed class HostUriComparer : UriComparer
        {
            internal static readonly UriComparer Value = new HostUriComparer();

            HostUriComparer()
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

            NoHostUriComparer()
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
        EndpointAddress address;
        WeakReference processorPool;

        int size;
        byte[] mask;
        Dictionary<QName, int> qnameLookup;
        Dictionary<string, HeaderBit[]> headerLookup;

        internal EndpointAddressMessageFilterHelper(EndpointAddress address)
        {
            this.address = address;

            if (this.address.Headers.Count > 0)
            {
                CreateMask();
                processorPool = new WeakReference(null);
            }
            else
            {
                qnameLookup = null;
                headerLookup = null;
                size = 0;
                mask = null;
            }
        }

        void CreateMask()
        {
            int nextBit = 0;
            string key;
            HeaderBit[] bits;
            QName qname;
            qnameLookup = new Dictionary<QName, int>(EndpointAddressProcessor.QNameComparer);
            headerLookup = new Dictionary<string, HeaderBit[]>();
            StringBuilder builder = null;

            for (int i = 0; i < address.Headers.Count; ++i)
            {
                if (builder == null)
                    builder = new StringBuilder();
                else
                    builder.Remove(0, builder.Length);


                key = address.Headers[i].GetComparableForm(builder);
                if (headerLookup.TryGetValue(key, out bits))
                {
                    Array.Resize(ref bits, bits.Length + 1);
                    bits[bits.Length - 1] = new HeaderBit(nextBit++);
                    headerLookup[key] = bits;
                }
                else
                {

                    headerLookup.Add(key, new HeaderBit[] { new HeaderBit(nextBit++) });
                    AddressHeader parameter = address.Headers[i];

                    qname.name = parameter.Name;
                    qname.ns = parameter.Namespace;
                    qnameLookup[qname] = 1;
                }
            }

            if (nextBit == 0)
            {
                size = 0;
            }
            else
            {
                size = (nextBit - 1) / 8 + 1;
            }

            if (size > 0)
            {
                mask = new byte[size];
                for (int i = 0; i < size - 1; ++i)
                {
                    mask[i] = 0xff;
                }

                if ((nextBit % 8) == 0)
                {
                    mask[size - 1] = 0xff;
                }
                else
                {
                    mask[size - 1] = (byte)((1 << (nextBit % 8)) - 1);
                }
            }
        }

        internal Dictionary<string, HeaderBit[]> HeaderLookup
        {
            get
            {
                if (headerLookup == null)
                    headerLookup = new Dictionary<string, HeaderBit[]>();
                return headerLookup;
            }
        }

        EndpointAddressProcessor CreateProcessor(int length)
        {
            if (processorPool.Target != null)
            {
                lock (processorPool)
                {
                    object o = processorPool.Target;
                    if (o != null)
                    {
                        EndpointAddressProcessor p = (EndpointAddressProcessor)o;
                        processorPool.Target = p.Next;
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
            if (size == 0)
            {
                return true;
            }

            EndpointAddressProcessor context = CreateProcessor(size);
            context.ProcessHeaders(message, qnameLookup, headerLookup);
            bool result = context.TestExact(mask);
            ReleaseProcessor(context);
            return result;
        }

        void ReleaseProcessor(EndpointAddressProcessor context)
        {
            lock (processorPool)
            {
                context.Next = processorPool.Target as EndpointAddressProcessor;
                processorPool.Target = context;
            }
        }

    }

}