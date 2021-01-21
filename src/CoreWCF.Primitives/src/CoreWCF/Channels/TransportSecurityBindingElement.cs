// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Security;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Channels
{
    public sealed class TransportSecurityBindingElement : SecurityBindingElement //, IPolicyExportExtension
    {
        public TransportSecurityBindingElement() : base()
        {
        }

        TransportSecurityBindingElement(TransportSecurityBindingElement elementToBeCloned) : base(elementToBeCloned)
        {
            // empty
        }

        internal override ISecurityCapabilities GetIndividualISecurityCapabilities()
        {
            bool supportsClientAuthentication;
            bool supportsClientWindowsIdentity;
            GetSupportingTokensCapabilities(out supportsClientAuthentication, out supportsClientWindowsIdentity);
            return new SecurityCapabilities(supportsClientAuthentication, false, supportsClientWindowsIdentity,
                ProtectionLevel.None, ProtectionLevel.None);
        }

        internal override bool SessionMode
        {
            get
            {
                SecureConversationSecurityTokenParameters scParameters = null;
                if (this.EndpointSupportingTokenParameters.Endorsing.Count > 0)
                    scParameters = this.EndpointSupportingTokenParameters.Endorsing[0] as SecureConversationSecurityTokenParameters;
                if (scParameters != null)
                    return scParameters.RequireCancellation;
                else
                    return false;
            }
        }

        internal override bool SupportsDuplex
        {
            get { return true; }
        }

        internal override bool SupportsRequestReply
        {
            get { return true; }
        }


        internal override SecurityProtocolFactory CreateSecurityProtocolFactory<TChannel>(BindingContext context, SecurityCredentialsManager credentialsManager, bool isForService, BindingContext issuerBindingContext)
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");
            if (credentialsManager == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("credentialsManager");

            TransportSecurityProtocolFactory protocolFactory = new TransportSecurityProtocolFactory();
            // if (isForService)
            //     base.ApplyAuditBehaviorSettings(context, protocolFactory);
            base.ConfigureProtocolFactory(protocolFactory, credentialsManager, isForService, issuerBindingContext, context.Binding);
            protocolFactory.DetectReplays = false;

            return protocolFactory;
        }

        protected override IServiceDispatcher BuildServiceDispatcherCore<TChannel>(BindingContext context, IServiceDispatcher serviceDispatcher)
        {
            SecurityServiceDispatcher securityServiceDispatcher = new SecurityServiceDispatcher(this, context, serviceDispatcher);
            SecurityCredentialsManager credentialsManager = serviceDispatcher.Host.Description.Behaviors.Find<SecurityCredentialsManager>();
            if (credentialsManager == null)
                credentialsManager = ServiceCredentials.CreateDefaultCredentials();

            SecureConversationSecurityTokenParameters scParameters;
            if (this.EndpointSupportingTokenParameters.Endorsing.Count > 0)
                scParameters = this.EndpointSupportingTokenParameters.Endorsing[0] as SecureConversationSecurityTokenParameters;
            else
                scParameters = null;

            bool requireDemuxer = RequiresChannelDemuxer();
            ChannelBuilder channelBuilder = new ChannelBuilder(context, requireDemuxer);

            if (requireDemuxer)
            {
                ApplyPropertiesOnDemuxer(channelBuilder, context);
            }

            BindingContext issuerBindingContext = context.Clone();
            issuerBindingContext.BindingParameters.Add(credentialsManager);
            if (scParameters != null)
            {
                if (scParameters.BootstrapSecurityBindingElement == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecureConversationSecurityTokenParametersRequireBootstrapBinding)));
                if (scParameters.RequireCancellation)
                {
                    SessionSymmetricTransportSecurityProtocolFactory sessionFactory = new SessionSymmetricTransportSecurityProtocolFactory();
                    // base.ApplyAuditBehaviorSettings(context, sessionFactory);
                    sessionFactory.SecurityTokenParameters = scParameters.Clone();
                    ((SecureConversationSecurityTokenParameters)sessionFactory.SecurityTokenParameters).IssuerBindingContext = issuerBindingContext;
                    this.EndpointSupportingTokenParameters.Endorsing.RemoveAt(0);
                    try
                    {
                        base.ConfigureProtocolFactory(sessionFactory, credentialsManager, true, issuerBindingContext, context.Binding);
                    }
                    finally
                    {
                        this.EndpointSupportingTokenParameters.Endorsing.Insert(0, scParameters);
                    }

                    securityServiceDispatcher.SessionMode = true;
                    securityServiceDispatcher.SessionServerSettings.InactivityTimeout = this.LocalServiceSettings.InactivityTimeout;
                    securityServiceDispatcher.SessionServerSettings.KeyRolloverInterval = this.LocalServiceSettings.SessionKeyRolloverInterval;
                    securityServiceDispatcher.SessionServerSettings.MaximumPendingSessions = this.LocalServiceSettings.MaxPendingSessions;
                    securityServiceDispatcher.SessionServerSettings.MaximumKeyRenewalInterval = this.LocalServiceSettings.SessionKeyRenewalInterval;
                    securityServiceDispatcher.SessionServerSettings.TolerateTransportFailures = this.LocalServiceSettings.ReconnectTransportOnFailure;
                    securityServiceDispatcher.SessionServerSettings.CanRenewSession = scParameters.CanRenewSession;
                    securityServiceDispatcher.SessionServerSettings.IssuedSecurityTokenParameters = scParameters.Clone();
                    ((SecureConversationSecurityTokenParameters)securityServiceDispatcher.SessionServerSettings.IssuedSecurityTokenParameters).IssuerBindingContext = issuerBindingContext;
                    securityServiceDispatcher.SessionServerSettings.SecurityStandardsManager = sessionFactory.StandardsManager;
                    securityServiceDispatcher.SessionServerSettings.SessionProtocolFactory = sessionFactory;
                    securityServiceDispatcher.SecurityProtocolFactory = sessionFactory;

                    // pass in the error handler for handling unknown security sessions - dont do this if the underlying channel is duplex since sending 
                    // back faults in response to badly secured requests over duplex can result in DoS.
                    //if (context.BindingParameters != null && context.BindingParameters.Find<IChannelDemuxFailureHandler>() == null
                    //    && !IsUnderlyingListenerDuplex<TChannel>(context))
                    //{
                    //    context.BindingParameters.Add(new SecuritySessionServerSettings.SecuritySessionDemuxFailureHandler(sessionFactory.StandardsManager));
                    //}
                }
                else
                {
                    //TODO later 
                    TransportSecurityProtocolFactory protocolFactory = new TransportSecurityProtocolFactory();
                    // base.ApplyAuditBehaviorSettings(context, protocolFactory);
                    this.EndpointSupportingTokenParameters.Endorsing.RemoveAt(0);
                    try
                    {
                        base.ConfigureProtocolFactory(protocolFactory, credentialsManager, true, issuerBindingContext, context.Binding);
                        SecureConversationSecurityTokenParameters acceleratedTokenParameters = (SecureConversationSecurityTokenParameters)scParameters.Clone();
                        acceleratedTokenParameters.IssuerBindingContext = issuerBindingContext;
                        protocolFactory.SecurityBindingElement.EndpointSupportingTokenParameters.Endorsing.Insert(0, acceleratedTokenParameters);
                    }
                    finally
                    {
                        this.EndpointSupportingTokenParameters.Endorsing.Insert(0, scParameters);
                    }

                    securityServiceDispatcher.SecurityProtocolFactory = protocolFactory;
                }

            }
            else
            {
                SecurityProtocolFactory protocolFactory = this.CreateSecurityProtocolFactory<TChannel>(context, credentialsManager, true, issuerBindingContext);
                securityServiceDispatcher.SecurityProtocolFactory = protocolFactory;
            }
            securityServiceDispatcher.InitializeSecurityDispatcher(channelBuilder, typeof(TChannel));
            //return channelListener;
            channelBuilder.BuildServiceDispatcher<TChannel>(context, securityServiceDispatcher);
            return securityServiceDispatcher;
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");

            if (typeof(T) == typeof(ChannelProtectionRequirements))
            {
                AddressingVersion addressing = MessageVersion.Default.Addressing;
                MessageEncodingBindingElement encoding = context.Binding.Elements.Find<MessageEncodingBindingElement>();
                if (encoding != null)
                {
                    addressing = encoding.MessageVersion.Addressing;
                }

                ChannelProtectionRequirements myRequirements = base.GetProtectionRequirements(addressing, ProtectionLevel.EncryptAndSign);
                myRequirements.Add(context.GetInnerProperty<ChannelProtectionRequirements>() ?? new ChannelProtectionRequirements());
                return (T)(object)myRequirements;
            }
            else
            {
                return base.GetProperty<T>(context);
            }
        }

        public override BindingElement Clone()
        {
            return new TransportSecurityBindingElement(this);
        }


    }
}
