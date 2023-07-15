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

        private TransportSecurityBindingElement(TransportSecurityBindingElement elementToBeCloned) : base(elementToBeCloned)
        {
            // empty
        }

        internal override ISecurityCapabilities GetIndividualISecurityCapabilities()
        {
            GetSupportingTokensCapabilities(out bool supportsClientAuthentication, out bool supportsClientWindowsIdentity);
            return new SecurityCapabilities(supportsClientAuthentication, false, supportsClientWindowsIdentity,
                ProtectionLevel.None, ProtectionLevel.None);
        }

        internal override bool SessionMode
        {
            get
            {
                SecureConversationSecurityTokenParameters scParameters = null;
                if (EndpointSupportingTokenParameters.Endorsing.Count > 0)
                {
                    scParameters = EndpointSupportingTokenParameters.Endorsing[0] as SecureConversationSecurityTokenParameters;
                }

                if (scParameters != null)
                {
                    return scParameters.RequireCancellation;
                }
                else
                {
                    return false;
                }
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
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (credentialsManager == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(credentialsManager));
            }

            TransportSecurityProtocolFactory protocolFactory = new TransportSecurityProtocolFactory();
            // if (isForService)
            //     base.ApplyAuditBehaviorSettings(context, protocolFactory);
            ConfigureProtocolFactory(protocolFactory, credentialsManager, isForService, issuerBindingContext, context.Binding);
            protocolFactory.DetectReplays = false;

            return protocolFactory;
        }

        protected override IServiceDispatcher BuildServiceDispatcherCore<TChannel>(BindingContext context, IServiceDispatcher serviceDispatcher)
        {
            SecurityServiceDispatcher securityServiceDispatcher = new SecurityServiceDispatcher(context, serviceDispatcher);
            SecurityCredentialsManager credentialsManager = serviceDispatcher.Host.Description.Behaviors.Find<SecurityCredentialsManager>();
            if (credentialsManager == null)
            {
                credentialsManager = ServiceCredentials.CreateDefaultCredentials();
            }

            SecureConversationSecurityTokenParameters scParameters;
            if (EndpointSupportingTokenParameters.Endorsing.Count > 0)
            {
                scParameters = EndpointSupportingTokenParameters.Endorsing[0] as SecureConversationSecurityTokenParameters;
            }
            else
            {
                scParameters = null;
            }

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
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecureConversationSecurityTokenParametersRequireBootstrapBinding)));
                }

                AddDemuxerForSecureConversation(channelBuilder, issuerBindingContext);

                if (scParameters.RequireCancellation)
                {
                    SessionSymmetricTransportSecurityProtocolFactory sessionFactory = new SessionSymmetricTransportSecurityProtocolFactory
                    {
                        // base.ApplyAuditBehaviorSettings(context, sessionFactory);
                        SecurityTokenParameters = scParameters.Clone()
                    };
                    ((SecureConversationSecurityTokenParameters)sessionFactory.SecurityTokenParameters).IssuerBindingContext = issuerBindingContext;
                    EndpointSupportingTokenParameters.Endorsing.RemoveAt(0);
                    try
                    {
                        ConfigureProtocolFactory(sessionFactory, credentialsManager, true, issuerBindingContext, context.Binding);
                    }
                    finally
                    {
                        EndpointSupportingTokenParameters.Endorsing.Insert(0, scParameters);
                    }

                    securityServiceDispatcher.SessionMode = true;
                    securityServiceDispatcher.SessionServerSettings.InactivityTimeout = LocalServiceSettings.InactivityTimeout;
                    securityServiceDispatcher.SessionServerSettings.KeyRolloverInterval = LocalServiceSettings.SessionKeyRolloverInterval;
                    securityServiceDispatcher.SessionServerSettings.MaximumPendingSessions = LocalServiceSettings.MaxPendingSessions;
                    securityServiceDispatcher.SessionServerSettings.MaximumKeyRenewalInterval = LocalServiceSettings.SessionKeyRenewalInterval;
                    securityServiceDispatcher.SessionServerSettings.TolerateTransportFailures = LocalServiceSettings.ReconnectTransportOnFailure;
                    securityServiceDispatcher.SessionServerSettings.CanRenewSession = scParameters.CanRenewSession;
                    securityServiceDispatcher.SessionServerSettings.IssuedSecurityTokenParameters = scParameters.Clone();
                    ((SecureConversationSecurityTokenParameters)securityServiceDispatcher.SessionServerSettings.IssuedSecurityTokenParameters).IssuerBindingContext = issuerBindingContext;
                    securityServiceDispatcher.SessionServerSettings.SecurityStandardsManager = sessionFactory.StandardsManager;
                    securityServiceDispatcher.SessionServerSettings.SessionProtocolFactory = sessionFactory;
                    securityServiceDispatcher.SecurityProtocolFactory = sessionFactory;

                    // pass in the error handler for handling unknown security sessions - dont do this if the underlying channel is duplex since sending 
                    // back faults in response to badly secured requests over duplex can result in DoS.
                    if (context.BindingParameters != null && context.BindingParameters.Find<IChannelDemuxFailureHandler>() == null
                        && !IsUnderlyingDispatcherDuplex<TChannel>(context))
                    {
                        context.BindingParameters.Add(new SecuritySessionServerSettings.SecuritySessionDemuxFailureHandler(sessionFactory.StandardsManager));
                    }
                }
                else
                {
                    //TODO later 
                    TransportSecurityProtocolFactory protocolFactory = new TransportSecurityProtocolFactory();
                    // base.ApplyAuditBehaviorSettings(context, protocolFactory);
                    EndpointSupportingTokenParameters.Endorsing.RemoveAt(0);
                    try
                    {
                        ConfigureProtocolFactory(protocolFactory, credentialsManager, true, issuerBindingContext, context.Binding);
                        SecureConversationSecurityTokenParameters acceleratedTokenParameters = (SecureConversationSecurityTokenParameters)scParameters.Clone();
                        acceleratedTokenParameters.IssuerBindingContext = issuerBindingContext;
                        protocolFactory.SecurityBindingElement.EndpointSupportingTokenParameters.Endorsing.Insert(0, acceleratedTokenParameters);
                    }
                    finally
                    {
                        EndpointSupportingTokenParameters.Endorsing.Insert(0, scParameters);
                    }

                    securityServiceDispatcher.SecurityProtocolFactory = protocolFactory;
                }
            }
            else
            {
                SecurityProtocolFactory protocolFactory = CreateSecurityProtocolFactory<TChannel>(context, credentialsManager, true, issuerBindingContext);
                securityServiceDispatcher.SecurityProtocolFactory = protocolFactory;
            }

            securityServiceDispatcher.InitializeSecurityDispatcher(channelBuilder, typeof(TChannel));

            // TODO: Find a way to I think I may have to make IServiceDispatcher dervive from ICommunicationObject and require OpenAsync to be called
            // through the chain of dispatchers. If that's the case, then that's a big API breaking change so will need to wait for .NET 8
            securityServiceDispatcher.OpenAsync().GetAwaiter().GetResult();
            //return channelListener;
            // I think this line does nothing of use, but leaving commented in case this does break something
            //channelBuilder.BuildServiceDispatcher<TChannel>(context, securityServiceDispatcher);
            return securityServiceDispatcher;
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(ChannelProtectionRequirements))
            {
                AddressingVersion addressing = MessageVersion.Default.Addressing;
                MessageEncodingBindingElement encoding = context.Binding.Elements.Find<MessageEncodingBindingElement>();
                if (encoding != null)
                {
                    addressing = encoding.MessageVersion.Addressing;
                }

                ChannelProtectionRequirements myRequirements = GetProtectionRequirements(addressing, ProtectionLevel.EncryptAndSign);
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
