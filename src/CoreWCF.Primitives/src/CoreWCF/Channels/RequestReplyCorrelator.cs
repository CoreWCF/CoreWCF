// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class RequestReplyCorrelator : IRequestReplyCorrelator
    {
        IDictionary<Key, object> states;

        internal RequestReplyCorrelator()
        {
            states = new Dictionary<Key, object>();
        }

        void IRequestReplyCorrelator.Add<T>(Message request, T state)
        {
            UniqueId messageId = request.Headers.MessageId;
            Type stateType = typeof(T);
            Key key = new Key(messageId, stateType);

            // add the correlator key to the request, this will be needed for cleaning up the correlator table in case of 
            // channel aborting or faulting while there are pending requests
            ICorrelatorKey value = state as ICorrelatorKey;
            if (value != null)
            {
                value.RequestCorrelatorKey = key;
            }

            lock (states)
            {
                states.Add(key, state);
            }
        }

        T IRequestReplyCorrelator.Find<T>(Message reply, bool remove)
        {
            UniqueId relatesTo = GetRelatesTo(reply);
            Type stateType = typeof(T);
            Key key = new Key(relatesTo, stateType);
            T value;

            lock (states)
            {
                value = (T)states[key];

                if (remove)
                    states.Remove(key);
            }

            return value;
        }

        // This method is used to remove the request from the correlator table when the
        // reply is lost. This will avoid leaking the correlator table in cases where the 
        // channel faults or aborts while there are pending requests.
        internal void RemoveRequest(ICorrelatorKey request)
        {
            Fx.Assert(request != null, "request cannot be null");
            if (request.RequestCorrelatorKey != null)
            {
                lock (states)
                {
                    states.Remove(request.RequestCorrelatorKey);
                }
            }
        }

        UniqueId GetRelatesTo(Message reply)
        {
            UniqueId relatesTo = reply.Headers.RelatesTo;
            if (relatesTo == null)
                throw TraceUtility.ThrowHelperError(new ArgumentException(SR.SuppliedMessageIsNotAReplyItHasNoRelatesTo0), reply);
            return relatesTo;
        }

        internal static bool AddressReply(Message reply, Message request)
        {
            ReplyToInfo info = RequestReplyCorrelator.ExtractReplyToInfo(request);
            return RequestReplyCorrelator.AddressReply(reply, info);
        }

        internal static bool AddressReply(Message reply, ReplyToInfo info)
        {
            EndpointAddress destination = null;

            if (info.HasFaultTo && (reply.IsFault))
            {
                destination = info.FaultTo;
            }
            else if (info.HasReplyTo)
            {
                destination = info.ReplyTo;
            }

            if (destination != null)
            {
                destination.ApplyTo(reply);
                return !destination.IsNone;
            }
            else
            {
                return true;
            }
        }

        internal static ReplyToInfo ExtractReplyToInfo(Message message)
        {
            return new ReplyToInfo(message);
        }

        internal static void PrepareRequest(Message request)
        {
            MessageHeaders requestHeaders = request.Headers;

            if (requestHeaders.MessageId == null)
            {
                requestHeaders.MessageId = new UniqueId();
            }

            request.Properties.AllowOutputBatching = false;
            //if (TraceUtility.PropagateUserActivity || TraceUtility.ShouldPropagateActivity)
            //{
            //    TraceUtility.AddAmbientActivityToMessage(request);
            //}
        }

        internal static void PrepareReply(Message reply, UniqueId messageId)
        {
            if (object.ReferenceEquals(messageId, null))
                throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.MissingMessageID), reply);

            MessageHeaders replyHeaders = reply.Headers;

            if (object.ReferenceEquals(replyHeaders.RelatesTo, null))
            {
                replyHeaders.RelatesTo = messageId;
            }

            //if (TraceUtility.PropagateUserActivity || TraceUtility.ShouldPropagateActivity)
            //{
            //    TraceUtility.AddAmbientActivityToMessage(reply);
            //}
        }

        internal static void PrepareReply(Message reply, Message request)
        {
            UniqueId messageId = request.Headers.MessageId;

            if (messageId != null)
            {
                MessageHeaders replyHeaders = reply.Headers;

                if (object.ReferenceEquals(replyHeaders.RelatesTo, null))
                {
                    replyHeaders.RelatesTo = messageId;
                }
            }

            //if (TraceUtility.PropagateUserActivity || TraceUtility.ShouldPropagateActivity)
            //{
            //    TraceUtility.AddAmbientActivityToMessage(reply);
            //}
        }

        internal struct ReplyToInfo
        {
            internal ReplyToInfo(Message message)
            {
                FaultTo = message.Headers.FaultTo;
                ReplyTo = message.Headers.ReplyTo;
                if (message.Version.Addressing == AddressingVersion.WSAddressingAugust2004)
                {
                    this.From = message.Headers.From;
                }
                else
                {
                    From = null;
                }
            }

            internal EndpointAddress FaultTo { get; }

            internal EndpointAddress From { get; }

            internal bool HasFaultTo
            {
                get { return !IsTrivial(FaultTo); }
            }

            internal bool HasFrom
            {
                get { return !IsTrivial(From); }
            }

            internal bool HasReplyTo
            {
                get { return !IsTrivial(ReplyTo); }
            }

            internal EndpointAddress ReplyTo { get; }

            bool IsTrivial(EndpointAddress address)
            {
                // Note: even if address.IsAnonymous, it may have identity, reference parameters, etc.
                return (address == null) || (address == EndpointAddress.AnonymousAddress);
            }
        }

        internal class Key
        {
            internal UniqueId MessageId;
            internal Type StateType;

            internal Key(UniqueId messageId, Type stateType)
            {
                MessageId = messageId;
                StateType = stateType;
            }

            public override bool Equals(object obj)
            {
                Key other = obj as Key;
                if (other == null)
                    return false;
                return other.MessageId == MessageId && other.StateType == StateType;
            }

            public override int GetHashCode()
            {
                return MessageId.GetHashCode() ^ StateType.GetHashCode();
            }

            public override string ToString()
            {
                return typeof(Key).ToString() + ": {" + MessageId + ", " + StateType.ToString() + "}";
            }
        }
    }
}