// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class SymmetricSecurityProtocolFactory : MessageSecurityProtocolFactory
    {
        private SecurityTokenParameters _tokenParameters;
        private SecurityTokenParameters _protectionTokenParameters;

        public SymmetricSecurityProtocolFactory()
            : base()
        {
        }

        internal SymmetricSecurityProtocolFactory(MessageSecurityProtocolFactory factory)
            : base(factory)
        {
        }

        public SecurityTokenParameters SecurityTokenParameters
        {
            get
            {
                return _tokenParameters;
            }
            set
            {
                ThrowIfImmutable();
                _tokenParameters = value;
            }
        }

        public SecurityTokenProvider RecipientAsymmetricTokenProvider { get; private set; }

        public SecurityTokenAuthenticator RecipientSymmetricTokenAuthenticator { get; private set; }

        public ReadOnlyCollection<SecurityTokenResolver> RecipientOutOfBandTokenResolverList { get; private set; }

        public override EndpointIdentity GetIdentityOfSelf()
        {
            EndpointIdentity identity;
            if (SecurityTokenManager is IEndpointIdentityProvider)
            {
                SecurityTokenRequirement requirement = CreateRecipientSecurityTokenRequirement();
                SecurityTokenParameters.InitializeSecurityTokenRequirement(requirement);
                identity = ((IEndpointIdentityProvider)SecurityTokenManager).GetIdentityOfSelf(requirement);
            }
            else
            {
                identity = base.GetIdentityOfSelf();
            }
            return identity;
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(Collection<ISecurityContextSecurityTokenCache>))
            {
                Collection<ISecurityContextSecurityTokenCache> result = base.GetProperty<Collection<ISecurityContextSecurityTokenCache>>();
                if (RecipientSymmetricTokenAuthenticator is ISecurityContextSecurityTokenCacheProvider)
                {
                    result.Add(((ISecurityContextSecurityTokenCacheProvider)RecipientSymmetricTokenAuthenticator).TokenCache);
                }
                return (T)(object)(result);
            }
            else
            {
                return base.GetProperty<T>();
            }
        }

        public override Task OnCloseAsync(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            if (!ActAsInitiator)
            {
                if (RecipientSymmetricTokenAuthenticator != null)
                {
                    SecurityUtils.CloseTokenAuthenticatorIfRequiredAsync(RecipientSymmetricTokenAuthenticator, timeoutHelper.GetCancellationToken());
                }
                if (RecipientAsymmetricTokenProvider != null)
                {
                    SecurityUtils.CloseTokenProviderIfRequiredAsync(RecipientAsymmetricTokenProvider, timeoutHelper.GetCancellationToken());
                }
            }
            return base.OnCloseAsync(timeoutHelper.RemainingTime());
        }

        public override void OnAbort()
        {
            if (!ActAsInitiator)
            {
                if (RecipientSymmetricTokenAuthenticator != null)
                {
                    SecurityUtils.AbortTokenAuthenticatorIfRequired(RecipientSymmetricTokenAuthenticator);
                }
                if (RecipientAsymmetricTokenProvider != null)
                {
                    SecurityUtils.AbortTokenProviderIfRequired(RecipientAsymmetricTokenProvider);
                }
            }
            base.OnAbort();
        }

       /* protected override SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, object listenerSecurityState, TimeSpan timeout)
        {
            return new SymmetricSecurityProtocol(this, target, via);
        }*/

        private RecipientServiceModelSecurityTokenRequirement CreateRecipientTokenRequirement()
        {
            RecipientServiceModelSecurityTokenRequirement requirement = CreateRecipientSecurityTokenRequirement();
            SecurityTokenParameters.InitializeSecurityTokenRequirement(requirement);
            requirement.KeyUsage = (SecurityTokenParameters.HasAsymmetricKey) ? SecurityKeyUsage.Exchange : SecurityKeyUsage.Signature;
            return requirement;
        }

        public override async Task OnOpenAsync(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            await base.OnOpenAsync(timeoutHelper.RemainingTime());

            if (_tokenParameters == null)
            {
                OnPropertySettingsError("SecurityTokenParameters", true);
            }

            if (!ActAsInitiator)
            {
                SecurityTokenRequirement recipientTokenRequirement = CreateRecipientTokenRequirement();
                SecurityTokenResolver resolver = null;
                if (SecurityTokenParameters.HasAsymmetricKey)
                {
                    RecipientAsymmetricTokenProvider = SecurityTokenManager.CreateSecurityTokenProvider(recipientTokenRequirement);
                }
                else
                {
                    RecipientSymmetricTokenAuthenticator = SecurityTokenManager.CreateSecurityTokenAuthenticator(recipientTokenRequirement, out resolver);
                }
                if (RecipientSymmetricTokenAuthenticator != null && RecipientAsymmetricTokenProvider != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.OnlyOneOfEncryptedKeyOrSymmetricBindingCanBeSelected)));
                }
                if (resolver != null)
                {
                    Collection<SecurityTokenResolver> tmp = new Collection<SecurityTokenResolver>();
                    tmp.Add(resolver);
                    RecipientOutOfBandTokenResolverList = new ReadOnlyCollection<SecurityTokenResolver>(tmp);
                }
                else
                {
                    RecipientOutOfBandTokenResolverList = EmptyReadOnlyCollection<SecurityTokenResolver>.Instance;
                }

                if (RecipientAsymmetricTokenProvider != null)
                {
                    Open("RecipientAsymmetricTokenProvider", true, RecipientAsymmetricTokenProvider, timeoutHelper.RemainingTime());
                }
                else
                {
                    Open("RecipientSymmetricTokenAuthenticator", true, RecipientSymmetricTokenAuthenticator, timeoutHelper.RemainingTime());
                }
            }
            if (_tokenParameters.RequireDerivedKeys)
            {
                ExpectKeyDerivation = true;
            }
            if (_tokenParameters.HasAsymmetricKey)
            {
                _protectionTokenParameters = new WrappedKeySecurityTokenParameters();
                _protectionTokenParameters.RequireDerivedKeys = SecurityTokenParameters.RequireDerivedKeys;
            }
            else
            {
                _protectionTokenParameters = _tokenParameters;
            }
        }

        internal SecurityTokenParameters GetProtectionTokenParameters()
        {
            return _protectionTokenParameters;
        }

        internal override SecurityProtocol OnCreateSecurityProtocol(EndpointAddress target, Uri via, TimeSpan timeout) => new SymmetricSecurityProtocol(this, target, via);
    }
}
