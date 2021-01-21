// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Principal;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public class SspiSecurityToken : SecurityToken
    {
        private string _id;
        private DateTime _effectiveTime;
        private DateTime _expirationTime;

        public SspiSecurityToken(TokenImpersonationLevel impersonationLevel, bool allowNtlm, NetworkCredential networkCredential)
        {
            ImpersonationLevel = impersonationLevel;
            AllowNtlm = allowNtlm;
            NetworkCredential = SecurityUtils.GetNetworkCredentialsCopy(networkCredential);
            _effectiveTime = DateTime.UtcNow;
            _expirationTime = _effectiveTime.AddHours(10);
        }

        public SspiSecurityToken(NetworkCredential networkCredential, bool extractGroupsForWindowsAccounts, bool allowUnauthenticatedCallers)
        {
            NetworkCredential = SecurityUtils.GetNetworkCredentialsCopy(networkCredential);
            ExtractGroupsForWindowsAccounts = extractGroupsForWindowsAccounts;
            AllowUnauthenticatedCallers = allowUnauthenticatedCallers;
            _effectiveTime = DateTime.UtcNow;
            _expirationTime = _effectiveTime.AddHours(10);
        }

        public override string Id
        {
            get
            {
                if (_id == null)
                    _id = SecurityUniqueId.Create().Value;
                return _id;
            }
        }

        public override DateTime ValidFrom => _effectiveTime;

        public override DateTime ValidTo => _expirationTime;

        public bool AllowUnauthenticatedCallers { get; } = SspiSecurityTokenProvider.DefaultAllowUnauthenticatedCallers;

        public TokenImpersonationLevel ImpersonationLevel { get; }

        public bool AllowNtlm { get; }

        public NetworkCredential NetworkCredential { get; }

        public bool ExtractGroupsForWindowsAccounts { get; }

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => EmptyReadOnlyCollection<SecurityKey>.Instance;
    }
}
