// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Security.Principal;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Claims
{
    public class WindowsClaimSet : ClaimSet, IIdentityInfo, IDisposable
    {
        internal const bool DefaultIncludeWindowsGroups = true;
        private readonly ClaimsIdentity _windowsIdentity;
        private readonly bool _includeWindowsGroups;
        private IList<Claim> _claims;
        private bool _disposed = false;
        private readonly string _authenticationType;
        GroupSidClaimCollection _groups;
        LdapSettings _ldapSettings;

        public WindowsClaimSet(WindowsIdentity windowsIdentity)
            : this(windowsIdentity, DefaultIncludeWindowsGroups)
        {
        }

        public WindowsClaimSet(WindowsIdentity windowsIdentity, bool includeWindowsGroups)
            : this(windowsIdentity, includeWindowsGroups, DateTime.UtcNow.AddHours(10))
        {
        }

        public WindowsClaimSet(WindowsIdentity windowsIdentity, DateTime expirationTime)
            : this(windowsIdentity, DefaultIncludeWindowsGroups, expirationTime)
        {
        }

        public WindowsClaimSet(WindowsIdentity windowsIdentity, bool includeWindowsGroups, DateTime expirationTime)
            : this(windowsIdentity, null, includeWindowsGroups, expirationTime, true)
        {
        }

        public WindowsClaimSet(WindowsIdentity windowsIdentity, string authenticationType, bool includeWindowsGroups, DateTime expirationTime)
            : this(windowsIdentity, authenticationType, includeWindowsGroups, expirationTime, true)
        {
        }

        internal WindowsClaimSet(WindowsIdentity windowsIdentity, string authenticationType, bool includeWindowsGroups, bool clone)
            : this(windowsIdentity, authenticationType, includeWindowsGroups, DateTime.UtcNow.AddHours(10), clone)
        {
        }

        internal WindowsClaimSet(WindowsIdentity windowsIdentity, string authenticationType, bool includeWindowsGroups, DateTime expirationTime, bool clone)
            : this(windowsIdentity, authenticationType, includeWindowsGroups, expirationTime, clone, null)
        {

        }

        internal WindowsClaimSet(WindowsIdentity windowsIdentity, string authenticationType, bool includeWindowsGroups, DateTime expirationTime, bool clone, IList<Claim> _fromClaims)
            : this(authenticationType, includeWindowsGroups, expirationTime, clone, _fromClaims)
        {
            if (windowsIdentity == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(windowsIdentity));
            }

            _windowsIdentity = clone ? SecurityUtils.CloneWindowsIdentityIfNecessary(windowsIdentity, authenticationType) : windowsIdentity;
        }

        internal WindowsClaimSet(ClaimsIdentity claimsIdentity, string authenticationType, bool includeWindowsGroups, DateTime expirationTime, bool clone, IList<Claim> _fromClaims, LdapSettings ldapSettings)
            : this(authenticationType, includeWindowsGroups, expirationTime, clone, _fromClaims)
        {
            if (claimsIdentity == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(claimsIdentity));
            }
            _windowsIdentity = (clone && claimsIdentity is WindowsIdentity) ? SecurityUtils.CloneWindowsIdentityIfNecessary((WindowsIdentity)claimsIdentity, authenticationType) : claimsIdentity;
            _ldapSettings = ldapSettings;
        }

       internal WindowsClaimSet(ClaimsIdentity claimsIdentity, bool includeWindowsGroups,  LdapSettings ldapSettings)
        : this(claimsIdentity, null, includeWindowsGroups, DateTime.UtcNow.AddHours(10), false, null,ldapSettings)
        {
        }

        private WindowsClaimSet(string authenticationType, bool includeWindowsGroups, DateTime expirationTime, bool clone, IList<Claim> _fromClaims)
        {
            _includeWindowsGroups = includeWindowsGroups;
            ExpirationTime = expirationTime;
            _authenticationType = authenticationType;
            if (_fromClaims != null && _fromClaims.Count > 0)
            {
                List<Claim> allClaims = new List<Claim>();
                foreach (Claim claim in _fromClaims)
                {
                    allClaims.Add(claim);
                }
                _claims = allClaims;
            }
        }

        private WindowsClaimSet(WindowsClaimSet from)
            : this(from.WindowsIdentity, from._authenticationType, from._includeWindowsGroups, from.ExpirationTime, true, from._claims, from._ldapSettings)
        {
        }

        public override Claim this[int index]
        {
            get
            {
                ThrowIfDisposed();
                EnsureClaims();
                return _claims[index];
            }
        }

        public override int Count
        {
            get
            {
                ThrowIfDisposed();
                EnsureClaims();
                return _claims.Count;
            }
        }

        IIdentity IIdentityInfo.Identity
        {
            get
            {
                ThrowIfDisposed();
                return _windowsIdentity;
            }
        }

        public ClaimsIdentity WindowsIdentity
        {
            get
            {
                ThrowIfDisposed();
                return _windowsIdentity;
            }
        }

        public override ClaimSet Issuer
        {
            get { return Windows; }
        }

        public DateTime ExpirationTime { get; }

        GroupSidClaimCollection Groups
        {
            get
            {
                if (this._groups == null)
                {
                    this._groups = new GroupSidClaimCollection(_windowsIdentity, _ldapSettings);
                }
                return this._groups;
            }
        }

        internal WindowsClaimSet Clone()
        {
            ThrowIfDisposed();
            return new WindowsClaimSet(this);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if(_windowsIdentity is WindowsIdentity)
                    ((WindowsIdentity)_windowsIdentity).Dispose();
            }
        }

        private IList<Claim> InitializeClaimsCore()
        {
            List<Claim> claims = new List<Claim>();
            if (_windowsIdentity is WindowsIdentity)
            {
                WindowsIdentity _windowsInternalIdentity = (WindowsIdentity)_windowsIdentity;
                if (_windowsInternalIdentity.AccessToken == null)
                {
                    return new List<Claim>();
                }

                claims.Add(new Claim(ClaimTypes.Sid, _windowsInternalIdentity.User, Rights.Identity));
                if (TryCreateWindowsSidClaim(_windowsInternalIdentity, out Claim claim))
                {
                    claims.Add(claim);
                }
                claims.Add(Claim.CreateNameClaim(_windowsIdentity.Name));
            }
            else
            {
                claims.Add(Claim.CreateNameClaim(_windowsIdentity.Name));
            }

            if (_includeWindowsGroups)
            {
                claims.AddRange(Groups);
            }
            return claims;
        }

        private void EnsureClaims()
        {
            if (_claims != null)
            {
                return;
            }

            _claims = InitializeClaimsCore();
        }

        public void AddClaim(Claim claim)
        {
            _claims.Add(claim);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }
        }

        private static bool SupportedClaimType(string claimType)
        {
            return claimType == null ||
                ClaimTypes.Sid == claimType ||
                ClaimTypes.DenyOnlySid == claimType ||
                ClaimTypes.Role == claimType ||
                ClaimTypes.Name == claimType;
        }

        // Note: null string represents any.
        public override IEnumerable<Claim> FindClaims(string claimType, string right)
        {
            ThrowIfDisposed();
            if (!SupportedClaimType(claimType) || !SupportedRight(right))
            {
                yield break;
            }
            else if (_claims == null && _windowsIdentity is WindowsIdentity && (ClaimTypes.Sid == claimType || ClaimTypes.DenyOnlySid == claimType))
            {
                if (ClaimTypes.Sid == claimType)
                {
                    if (right == null || Rights.Identity == right)
                    {
                        yield return new Claim(ClaimTypes.Sid, ((WindowsIdentity)_windowsIdentity).User, Rights.Identity);
                    }
                }

                if (right == null || Rights.PossessProperty == right)
                {
                    if (TryCreateWindowsSidClaim((WindowsIdentity)_windowsIdentity, out Claim sid))
                    {
                        if (claimType == sid.ClaimType)
                        {
                            yield return sid;
                        }
                    }
                }

                if (_includeWindowsGroups && (right == null || Rights.PossessProperty == right))
                {
                    for (int i = 0; i < this.Groups.Count; ++i)
                    {
                        Claim sid = this.Groups[i];
                        if (claimType == sid.ClaimType)
                        {
                            yield return sid;
                        }
                    }
                }
            }
            else
            {
                EnsureClaims();

                bool anyClaimType = (claimType == null);
                bool anyRight = (right == null);

                for (int i = 0; i < _claims.Count; ++i)
                {
                    Claim claim = _claims[i];
                    if ((claim != null) &&
                        (anyClaimType || claimType == claim.ClaimType) &&
                        (anyRight || right == claim.Right))
                    {
                        yield return claim;
                    }
                }
            }
        }

        public override IEnumerator<Claim> GetEnumerator()
        {
            ThrowIfDisposed();
            EnsureClaims();
            return _claims.GetEnumerator();
        }

        public override string ToString()
        {
            return _disposed ? base.ToString() : SecurityUtils.ClaimSetToString(this);
        }

        public static bool TryCreateWindowsSidClaim(WindowsIdentity windowsIdentity, out Claim claim)
        {
            if (windowsIdentity.User != null && windowsIdentity.User.IsAccountSid())
            {
                claim = Claim.CreateWindowsSidClaim(new SecurityIdentifier(windowsIdentity.User.Value));
                return true;
            }
            claim = null;
            return false;
        }

        class GroupSidClaimCollection : Collection<Claim>
        {
            public GroupSidClaimCollection(ClaimsIdentity claimsIdentity, LdapSettings ldapSettings)
            {
                if(claimsIdentity is WindowsIdentity)
                {
                    var windowsIdentity = (WindowsIdentity)claimsIdentity;
                    if (windowsIdentity.Token != IntPtr.Zero)
                    {
                        foreach (var groupId in windowsIdentity.Groups)
                        {
                            var group = groupId.Translate(typeof(NTAccount));
                            string[] domainGroups = group.Value.Split(new char[] { '\\' });
                            if (domainGroups.Length > 1)
                            {
                                base.Add(new Claim(ClaimTypes.Role, domainGroups[1], Rights.Identity));
                            }
                            else
                            {
                                base.Add(new Claim(ClaimTypes.Role, group, Rights.Identity));
                            }
                        }
                    }
                }
                else if(ldapSettings !=null)
                {
                    List<Claim> allClaims = LdapAdapter.RetrieveClaimsAsync(ldapSettings, claimsIdentity.Name).GetAwaiter().GetResult();
                    foreach(Claim roleClaim in allClaims)
                    {
                        base.Add(roleClaim);
                    }
                }

            }
        }
    }
}
