// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;

namespace CoreWCF.Security
{
    /// <summary>
    /// The helper class to enable impersonation while serializing the body of the reply message.
    /// </summary>
    public class ImpersonateOnSerializingReplyMessageProperty : IMessageProperty
    {
        const string PropertyName = "ImpersonateOnSerializingReplyMessageProperty";
        MessageRpc _rpc;

        internal ImpersonateOnSerializingReplyMessageProperty(MessageRpc rpc)
        {
            _rpc = rpc;
        }

        /// <summary>
        /// Gets the name of the message property.
        /// </summary>
        public static string Name
        {
            get { return PropertyName; }
        }

        /// <summary>
        /// Gets the ImpersonateOnSerializingReplyMessageProperty property from a message.
        /// </summary>
        /// <param name="message">The message to extract the property from.</param>
        /// <param name="property">An output paramter to hold the ImpersonateOnSerializingReplyMessageProperty property.</param>
        /// <returns>True if the ImpersonateOnSerializingReplyMessageProperty property was found.</returns>
        public static bool TryGet(Message message, out ImpersonateOnSerializingReplyMessageProperty property)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            return TryGet(message.Properties, out property);
        }

        /// <summary>
        /// Gets the ImpersonateOnSerializingReplyMessageProperty property from MessageProperties.
        /// </summary>
        /// <param name="properties">The MessagePropeties object.</param>
        /// <param name="property">An output paramter to hold the ImpersonateOnSerializingReplyMessageProperty property.</param>
        /// <returns>True if the ImpersonateOnSerializingReplyMessageProperty property was found.</returns>
        public static bool TryGet(MessageProperties properties, out ImpersonateOnSerializingReplyMessageProperty property)
        {
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(properties));
            }

            object value = null;
            if (properties.TryGetValue(PropertyName, out value))
            {
                property = value as ImpersonateOnSerializingReplyMessageProperty;
            }
            else
            {
                property = null;
            }

            return property != null;
        }

        /// <summary>
        /// Creates a copy of the message property.
        /// </summary>
        /// <returns>Returns a copy of the message property.</returns>
        public IMessageProperty CreateCopy()
        {
            return new ImpersonateOnSerializingReplyMessageProperty(_rpc);
        }

        /// <summary>
        /// Executes a Func<typeparamref name="T"/> with the caller's context if impersonation is enabled on the service and sets the appropriate principal on the thread as per the service configuration.
        /// </summary>
        /// <param name="func">The function to execute under caller's impersonated context</param>
        /// <returns>The return value from executing the func</returns>
        public T RunImpersonated<T>(Func<T> func)
        {
            if (OperationContext.Current != null)
            {
                EndpointDispatcher endpointDispatcher = OperationContext.Current.EndpointDispatcher;
                if (endpointDispatcher != null)
                {
                    DispatchRuntime dispatchRuntime = endpointDispatcher.DispatchRuntime;
                    ImmutableDispatchRuntime runtime = dispatchRuntime.GetRuntime();
                    if (runtime?.SecurityImpersonation?.IsSecurityContextImpersonationRequired(_rpc) ?? false)
                    {
                        return runtime.SecurityImpersonation.RunImpersonated(_rpc, func);
                    }
                }
            }

            return func();
        }
    }
}
