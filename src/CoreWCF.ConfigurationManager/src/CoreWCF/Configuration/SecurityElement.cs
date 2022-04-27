// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Configuration;
using CoreWCF.Channels;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Configuration
{
    public sealed class SecurityElement : SecurityElementBase
    {
        public SecurityElement()
        {
            SecureConversationBootstrap.IsSecurityElementBootstrap = true; // Tell the bootstrap it's potentially okay to optimize itself out of config representation
        }

        [ConfigurationProperty(ConfigurationStrings.SecureConversationBootstrap)]
        public SecurityElementBase SecureConversationBootstrap
        {
            get { return (SecurityElementBase)base[ConfigurationStrings.SecureConversationBootstrap]; }
        }

        public override void CopyFrom(ServiceModelExtensionElement from)
        {
            base.CopyFrom(from);

            SecurityElement source = (SecurityElement)from;
            
            if (PropertyValueOrigin.Default != source.ElementInformation.Properties[ConfigurationStrings.SecureConversationBootstrap].ValueOrigin)
                SecureConversationBootstrap.CopyFrom(source.SecureConversationBootstrap);
        }

        protected internal override BindingElement CreateBindingElement(bool createTemplateOnly)
        {
            SecurityBindingElement result;
            if (AuthenticationMode == AuthenticationMode.SecureConversation)
            {
                if (SecureConversationBootstrap == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecureConversationNeedsBootstrapSecurity)));
                if (SecureConversationBootstrap.AuthenticationMode == AuthenticationMode.SecureConversation)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecureConversationBootstrapCannotUseSecureConversation)));
                SecurityBindingElement bootstrapSecurity = (SecurityBindingElement)SecureConversationBootstrap.CreateBindingElement(createTemplateOnly);
                result = SecurityBindingElement.CreateSecureConversationBindingElement(bootstrapSecurity, RequireSecurityContextCancellation);
            }
            else
            {
                result = (SecurityBindingElement)base.CreateBindingElement(createTemplateOnly);
            }

            ApplyConfiguration(result);

            return result;
        }

        protected override void AddBindingTemplates(Dictionary<AuthenticationMode, SecurityBindingElement> bindingTemplates)
        {
            base.AddBindingTemplates(bindingTemplates);
            AddBindingTemplate(bindingTemplates, AuthenticationMode.SecureConversation);
        }

        private void InitializeSecureConversationParameters(SecureConversationSecurityTokenParameters sc, bool initializeNestedBindings)
        {
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.RequireSecurityContextCancellation, sc.RequireCancellation);
            CanRenewSecurityContextToken = sc.CanRenewSession; // can't use default value optimization here because ApplyConfiguration relies on the runtime default instead, which is the opposite of the config default
            if (sc.BootstrapSecurityBindingElement != null)
            {
                SecureConversationBootstrap.InitializeFrom(sc.BootstrapSecurityBindingElement, initializeNestedBindings);
            }
        }

        protected override void InitializeNestedTokenParameterSettings(SecurityTokenParameters sp, bool initializeNestedBindings)
        {
            if (sp is SecureConversationSecurityTokenParameters)
                InitializeSecureConversationParameters((SecureConversationSecurityTokenParameters)sp, initializeNestedBindings);
            else
                base.InitializeNestedTokenParameterSettings(sp, initializeNestedBindings);
        }
    }
}
