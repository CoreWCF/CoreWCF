// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal static class ChannelBindingMessagePropertyHelper
    {
        internal static bool TryGet(Message message, out ChannelBindingMessageProperty property)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            return TryGet(message.Properties, out property);
        }

        internal static bool TryGet(MessageProperties properties, out ChannelBindingMessageProperty property)
        {
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(properties));
            }

            property = null;

            if (properties.TryGetValue(ChannelBindingMessageProperty.Name, out object value))
            {
                property = value as ChannelBindingMessageProperty;
                return property != null;
            }

            return false;
        }

        internal static void AddTo(this ChannelBindingMessageProperty channelBindingProperty, Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            channelBindingProperty.AddTo(message.Properties);
        }

        internal static void AddTo(this ChannelBindingMessageProperty channelBindingProperty, MessageProperties properties)
        {
            // Throws if disposed
            System.Security.Authentication.ExtendedProtection.ChannelBinding dummy = channelBindingProperty.ChannelBinding;
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(properties));
            }

            properties.Add(ChannelBindingMessageProperty.Name, channelBindingProperty);
        }
    }
}
