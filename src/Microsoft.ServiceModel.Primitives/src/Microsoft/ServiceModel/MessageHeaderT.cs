using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel
{
    public class MessageHeader<T>
    {
        string actor;
        bool mustUnderstand;
        bool relay;
        T content;

        public MessageHeader()
        {
        }

        public MessageHeader(T content)
            : this(content, false, "", false)
        {
        }

        public MessageHeader(T content, bool mustUnderstand, string actor, bool relay)
        {
            this.content = content;
            this.mustUnderstand = mustUnderstand;
            this.actor = actor;
            this.relay = relay;
        }

        public string Actor
        {
            get { return actor; }
            set { actor = value; }
        }

        public T Content
        {
            get { return content; }
            set { content = value; }
        }

        public bool MustUnderstand
        {
            get { return mustUnderstand; }
            set { mustUnderstand = value; }
        }

        public bool Relay
        {
            get { return relay; }
            set { relay = value; }
        }

        internal Type GetGenericArgument()
        {
            return typeof(T);
        }

        public MessageHeader GetUntypedHeader(string name, string ns)
        {
            return MessageHeader.CreateHeader(name, ns, content, mustUnderstand, actor, relay);
        }
    }

    // problem: creating / getting content / settings content on a MessageHeader<T> given the type at runtime
    // require reflection.
    // solution: This class creates a cache of adapters that provide an untyped wrapper over a particular
    // MessageHeader<T> instantiation.
    // better solution: implement something like "IUntypedTypedHeader" that has a "object Content" property,
    // privately implement this on TypedHeader, and then just use that iface to operation on the header (actually
    // you'd still have the creation problem...).  the issue with that is you now have a new public interface
    internal abstract class TypedHeaderManager
    {
        static Dictionary<Type, TypedHeaderManager> cache = new Dictionary<Type, TypedHeaderManager>();
        static ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
        static Type GenericAdapterType = typeof(GenericAdapter<>);

        internal static object Create(Type t, object content, bool mustUnderstand, bool relay, string actor)
        {
            return GetTypedHeaderManager(t).Create(content, mustUnderstand, relay, actor);
        }

        internal static object GetContent(Type t, object typedHeaderInstance, out bool mustUnderstand, out bool relay, out string actor)
        {
            return GetTypedHeaderManager(t).GetContent(typedHeaderInstance, out mustUnderstand, out relay, out actor);
        }

        internal static Type GetMessageHeaderType(Type contentType)
        {
            return GetTypedHeaderManager(contentType).GetMessageHeaderType();
        }
        internal static Type GetHeaderType(Type headerParameterType)
        {
            if (headerParameterType.GetTypeInfo().IsGenericType && headerParameterType.GetGenericTypeDefinition() == typeof(MessageHeader<>))
                return headerParameterType.GetGenericArguments()[0];
            return headerParameterType;
        }

        static TypedHeaderManager GetTypedHeaderManager(Type t)
        {
            TypedHeaderManager result = null;

            bool readerLockHeld = false;
            bool writerLockHeld = false;
            try
            {
                try
                {
                }
                finally
                {
                    cacheLock.TryEnterUpgradeableReadLock(Timeout.Infinite);
                    readerLockHeld = true;
                }
                if (!cache.TryGetValue(t, out result))
                {
                    cacheLock.TryEnterWriteLock(Timeout.Infinite);
                    writerLockHeld = true;
                    if (!cache.TryGetValue(t, out result))
                    {
                        result = (TypedHeaderManager) Activator.CreateInstance(GenericAdapterType.MakeGenericType(t));
                        cache.Add(t, result);
                    }
                }
            }
            finally
            {
                if (writerLockHeld)
                {
                    cacheLock.ExitWriteLock();
                }
                if (readerLockHeld)
                {
                    cacheLock.ExitUpgradeableReadLock();
                }
            }

            return result;
        }

        protected abstract object Create(object content, bool mustUnderstand, bool relay, string actor);
        protected abstract object GetContent(object typedHeaderInstance, out bool mustUnderstand, out bool relay, out string actor);
        protected abstract Type GetMessageHeaderType();

        class GenericAdapter<T> : TypedHeaderManager
        {
            protected override object Create(object content, bool mustUnderstand, bool relay, string actor)
            {
                MessageHeader<T> header = new MessageHeader<T>();
                header.Content = (T)content;
                header.MustUnderstand = mustUnderstand;
                header.Relay = relay;
                header.Actor = actor;
                return header;
            }

            protected override object GetContent(object typedHeaderInstance, out bool mustUnderstand, out bool relay, out string actor)
            {
                mustUnderstand = false;
                relay = false;
                actor = null;
                if (typedHeaderInstance == null)
                    return null;

                MessageHeader<T> header = typedHeaderInstance as MessageHeader<T>;
                if (header == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException("typedHeaderInstance"));
                mustUnderstand = header.MustUnderstand;
                relay = header.Relay;
                actor = header.Actor;
                return header.Content;
            }

            protected override Type GetMessageHeaderType()
            {
                return typeof(MessageHeader<T>);
            }
        }
    }

}