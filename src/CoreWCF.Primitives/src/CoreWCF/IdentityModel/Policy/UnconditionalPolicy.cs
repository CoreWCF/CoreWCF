// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Principal;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Policy
{
    internal interface IIdentityInfo
    {
        IIdentity Identity { get; }
    }

    internal class UnconditionalPolicy : IAuthorizationPolicy, IDisposable
    {
        private SecurityUniqueId id;
        private ClaimSet issuance;
        private ReadOnlyCollection<ClaimSet> issuances;
        private IIdentity primaryIdentity;
        private bool disposed = false;

        public UnconditionalPolicy(ClaimSet issuance)
            : this(issuance, SecurityUtils.MaxUtcDateTime)
        {
        }

        public UnconditionalPolicy(ClaimSet issuance, DateTime expirationTime)
        {
            if (issuance == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(issuance));
            }

            Initialize(ClaimSet.System, issuance, null, expirationTime);
        }

        public UnconditionalPolicy(ReadOnlyCollection<ClaimSet> issuances, DateTime expirationTime)
        {
            if (issuances == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(issuances));
            }

            Initialize(ClaimSet.System, null, issuances, expirationTime);
        }

        internal UnconditionalPolicy(IIdentity primaryIdentity, ClaimSet issuance)
            : this(issuance)
        {
            this.primaryIdentity = primaryIdentity;
        }

        internal UnconditionalPolicy(IIdentity primaryIdentity, ClaimSet issuance, DateTime expirationTime)
            : this(issuance, expirationTime)
        {
            this.primaryIdentity = primaryIdentity;
        }

        internal UnconditionalPolicy(IIdentity primaryIdentity, ReadOnlyCollection<ClaimSet> issuances, DateTime expirationTime)
            : this(issuances, expirationTime)
        {
            this.primaryIdentity = primaryIdentity;
        }

        private UnconditionalPolicy(UnconditionalPolicy from)
        {
            IsDisposable = from.IsDisposable;
            primaryIdentity = from.IsDisposable ? SecurityUtils.CloneIdentityIfNecessary(from.primaryIdentity) : from.primaryIdentity;
            if (from.issuance != null)
            {
                issuance = from.IsDisposable ? SecurityUtils.CloneClaimSetIfNecessary(from.issuance) : from.issuance;
            }
            else
            {
                issuances = from.IsDisposable ? SecurityUtils.CloneClaimSetsIfNecessary(from.issuances) : from.issuances;
            }
            Issuer = from.Issuer;
            ExpirationTime = from.ExpirationTime;
        }

        private void Initialize(ClaimSet issuer, ClaimSet issuance, ReadOnlyCollection<ClaimSet> issuances, DateTime expirationTime)
        {
            Issuer = issuer;
            this.issuance = issuance;
            this.issuances = issuances;
            ExpirationTime = expirationTime;
            if (issuance != null)
            {
                IsDisposable = issuance is WindowsClaimSet;
            }
            else
            {
                for (int i = 0; i < issuances.Count; ++i)
                {
                    if (issuances[i] is WindowsClaimSet)
                    {
                        IsDisposable = true;
                        break;
                    }
                }
            }
        }

        public string Id
        {
            get
            {
                if (id == null)
                {
                    id = SecurityUniqueId.Create();
                }

                return id.Value;
            }
        }

        public ClaimSet Issuer { get; private set; }

        internal IIdentity PrimaryIdentity
        {
            get
            {
                ThrowIfDisposed();
                if (primaryIdentity == null)
                {
                    IIdentity identity = null;
                    if (issuance != null)
                    {
                        if (issuance is IIdentityInfo)
                        {
                            identity = ((IIdentityInfo)issuance).Identity;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < issuances.Count; ++i)
                        {
                            ClaimSet issuance = issuances[i];
                            if (issuance is IIdentityInfo)
                            {
                                identity = ((IIdentityInfo)issuance).Identity;
                                // Preferably Non-Anonymous
                                if (identity != null && identity != SecurityUtils.AnonymousIdentity)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    primaryIdentity = identity ?? SecurityUtils.AnonymousIdentity;
                }
                return primaryIdentity;
            }
        }

        internal ReadOnlyCollection<ClaimSet> Issuances
        {
            get
            {
                ThrowIfDisposed();
                if (issuances == null)
                {
                    List<ClaimSet> issuances = new List<ClaimSet>(1);
                    issuances.Add(issuance);
                    this.issuances = issuances.AsReadOnly();
                }
                return issuances;
            }
        }

        public DateTime ExpirationTime { get; private set; }

        internal bool IsDisposable { get; private set; } = false;

        internal UnconditionalPolicy Clone()
        {
            ThrowIfDisposed();
            return (IsDisposable) ? new UnconditionalPolicy(this) : this;
        }

        public virtual void Dispose()
        {
            if (IsDisposable && !disposed)
            {
                disposed = true;
                SecurityUtils.DisposeIfNecessary(primaryIdentity as WindowsIdentity);
                SecurityUtils.DisposeClaimSetIfNecessary(issuance);
                SecurityUtils.DisposeClaimSetsIfNecessary(issuances);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }

        public virtual bool Evaluate(EvaluationContext evaluationContext, ref object state)
        {
            ThrowIfDisposed();
            if (issuance != null)
            {
                evaluationContext.AddClaimSet(this, issuance);
            }
            else
            {
                for (int i = 0; i < issuances.Count; ++i)
                {
                    if (issuances[i] != null)
                    {
                        evaluationContext.AddClaimSet(this, issuances[i]);
                    }
                }
            }

            // Preferably Non-Anonymous
            if (PrimaryIdentity != null && PrimaryIdentity != SecurityUtils.AnonymousIdentity)
            {
                IList<IIdentity> identities;
                if (!evaluationContext.Properties.TryGetValue(SecurityUtils.Identities, out object obj))
                {
                    identities = new List<IIdentity>(1);
                    evaluationContext.Properties.Add(SecurityUtils.Identities, identities);
                }
                else
                {
                    // null if other overrides the property with something else
                    identities = obj as IList<IIdentity>;
                }

                if (identities != null)
                {
                    identities.Add(PrimaryIdentity);
                }
            }

            evaluationContext.RecordExpirationTime(ExpirationTime);
            return true;
        }
    }

}
