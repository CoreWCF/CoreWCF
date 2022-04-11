// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;

namespace CoreWCF.Configuration
{
    internal class MessageSecurityVersionConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (typeof(string) == sourceType)
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (typeof(InstanceDescriptor) == destinationType)
            {
                return true;
            }
            return base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is string)
            {
                string version = (string)value;
                MessageSecurityVersion retval;
                switch (version)
                {
                    case ConfigurationStrings.WsSecurity11WsTrustFebruary2005WsSecureConversationFebruary2005WsSecurityPolicy11:
                        retval = MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11;
                        break;
                    case ConfigurationStrings.WsSecurity10WsTrustFebruary2005WsSecureConversationFebruary2005WsSecurityPolicy11BasicSecurityProfile10:
                        retval = MessageSecurityVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10;
                        break;
                    case ConfigurationStrings.WsSecurity11WsTrustFebruary2005WsSecureConversationFebruary2005WsSecurityPolicy11BasicSecurityProfile10:
                        retval = MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10;
                        break;
                    case ConfigurationStrings.WsSecurity10WsTrust13WsSecureConversation13WsSecurityPolicy12BasicSecurityProfile10:
                        retval = MessageSecurityVersion.WSSecurity10WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10;
                        break;
                    case ConfigurationStrings.WsSecurity11WsTrust13WsSecureConversation13WsSecurityPolicy12:
                        retval = MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12;
                        break;
                    case ConfigurationStrings.WsSecurity11WsTrust13WsSecureConversation13WsSecurityPolicy12BasicSecurityProfile10:
                        retval = MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10;
                        break;
                    case ConfigurationStrings.Default:
                        retval = MessageSecurityVersion.Default;
                        break;
                    default:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value",
                            SR.Format(SR.ConfigInvalidClassFactoryValue, version, typeof(MessageSecurityVersion).FullName)));
                }
                return retval;
            }
            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (typeof(string) == destinationType && value is MessageSecurityVersion)
            {
                string retval;
                MessageSecurityVersion securityVersion = (MessageSecurityVersion)value;
                if (securityVersion == MessageSecurityVersion.Default)
                {
                    retval = ConfigurationStrings.Default;
                }
                else if (securityVersion == MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11)
                {
                    retval = ConfigurationStrings.WsSecurity11WsTrustFebruary2005WsSecureConversationFebruary2005WsSecurityPolicy11;
                }
                else if (securityVersion == MessageSecurityVersion.WSSecurity10WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10)
                {
                    retval = ConfigurationStrings.WsSecurity10WsTrustFebruary2005WsSecureConversationFebruary2005WsSecurityPolicy11BasicSecurityProfile10;
                }
                else if (securityVersion == MessageSecurityVersion.WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10)
                {
                    retval = ConfigurationStrings.WsSecurity11WsTrustFebruary2005WsSecureConversationFebruary2005WsSecurityPolicy11BasicSecurityProfile10;
                }
                else if (securityVersion == MessageSecurityVersion.WSSecurity10WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10)
                {
                    retval = ConfigurationStrings.WsSecurity10WsTrust13WsSecureConversation13WsSecurityPolicy12BasicSecurityProfile10;
                }
                else if (securityVersion == MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12)
                {
                    retval = ConfigurationStrings.WsSecurity11WsTrust13WsSecureConversation13WsSecurityPolicy12;
                }
                else if (securityVersion == MessageSecurityVersion.WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10)
                {
                    retval = ConfigurationStrings.WsSecurity11WsTrust13WsSecureConversation13WsSecurityPolicy12BasicSecurityProfile10;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value",
                        SR.Format(SR.ConfigInvalidClassInstanceValue, typeof(MessageSecurityVersion).FullName)));
                }
                return retval;
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
