// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    internal class ScopedMessagePartSpecification
    {
        MessagePartSpecification channelParts;
        Dictionary<string, MessagePartSpecification> actionParts;
        Dictionary<string, MessagePartSpecification> readOnlyNormalizedActionParts;
        bool isReadOnly;

        public ScopedMessagePartSpecification()
        {
            channelParts = new MessagePartSpecification();
            actionParts = new Dictionary<string, MessagePartSpecification>();
        }

        public ICollection<string> Actions
        {
            get
            {
                return actionParts.Keys;
            }
        }

        public MessagePartSpecification ChannelParts
        {
            get
            {
                return channelParts;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return isReadOnly;
            }
        }

        public ScopedMessagePartSpecification(ScopedMessagePartSpecification other)
            : this()
        {
            if (other == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(other)));

            channelParts.Union(other.channelParts);
            if (other.actionParts != null)
            {
                foreach (string action in other.actionParts.Keys)
                {
                    MessagePartSpecification p = new MessagePartSpecification();
                    p.Union(other.actionParts[action]);
                    actionParts[action] = p;
                }
            }
        }

        internal ScopedMessagePartSpecification(ScopedMessagePartSpecification other, bool newIncludeBody)
            : this(other)
        {
            channelParts.IsBodyIncluded = newIncludeBody;
            foreach (string action in actionParts.Keys)
                actionParts[action].IsBodyIncluded = newIncludeBody;
        }

        public void AddParts(MessagePartSpecification parts)
        {
            if (parts == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(parts)));

            ThrowIfReadOnly();

            channelParts.Union(parts);
        }

        public void AddParts(MessagePartSpecification parts, string action)
        {
            if (action == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(action)));
            if (parts == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(parts)));

            ThrowIfReadOnly();

            if (!actionParts.ContainsKey(action))
                actionParts[action] = new MessagePartSpecification();
            actionParts[action].Union(parts);
        }

        internal void AddParts(MessagePartSpecification parts, XmlDictionaryString action)
        {
            if (action == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(action)));
            AddParts(parts, action.Value);
        }

        internal bool IsEmpty()
        {
            bool result;
            if (!channelParts.IsEmpty())
            {
                result = false;
            }
            else
            {
                result = true;
                foreach (string action in Actions)
                {
                    MessagePartSpecification parts;
                    if (TryGetParts(action, true, out parts))
                    {
                        if (!parts.IsEmpty())
                        {
                            result = false;
                            break;
                        }
                    }
                }
            }

            return result;
        }

        public bool TryGetParts(string action, bool excludeChannelScope, out MessagePartSpecification parts)
        {
            if (action == null)
                action = MessageHeaders.WildcardAction;
            parts = null;

            if (isReadOnly)
            {
                if (readOnlyNormalizedActionParts.ContainsKey(action))
                    if (excludeChannelScope)
                        parts = actionParts[action];
                    else
                        parts = readOnlyNormalizedActionParts[action];
            }
            else if (actionParts.ContainsKey(action))
            {
                MessagePartSpecification p = new MessagePartSpecification();
                p.Union(actionParts[action]);
                if (!excludeChannelScope)
                    p.Union(channelParts);
                parts = p;
            }

            return parts != null;
        }

        internal void CopyTo(ScopedMessagePartSpecification target)
        {
            if (target == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(target));
            }
            target.ChannelParts.IsBodyIncluded = ChannelParts.IsBodyIncluded;
            foreach (XmlQualifiedName headerType in ChannelParts.HeaderTypes)
            {
                if (!target.channelParts.IsHeaderIncluded(headerType.Name, headerType.Namespace))
                {
                    target.ChannelParts.HeaderTypes.Add(headerType);
                }
            }
            foreach (string action in actionParts.Keys)
            {
                target.AddParts(actionParts[action], action);
            }
        }

        public bool TryGetParts(string action, out MessagePartSpecification parts)
        {
            return TryGetParts(action, false, out parts);
        }

        public void MakeReadOnly()
        {
            if (!isReadOnly)
            {
                readOnlyNormalizedActionParts = new Dictionary<string, MessagePartSpecification>();
                foreach (string action in actionParts.Keys)
                {
                    MessagePartSpecification p = new MessagePartSpecification();
                    p.Union(actionParts[action]);
                    p.Union(channelParts);
                    p.MakeReadOnly();
                    readOnlyNormalizedActionParts[action] = p;
                }
                isReadOnly = true;
            }
        }

        void ThrowIfReadOnly()
        {
            if (isReadOnly)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
        }
    }

}