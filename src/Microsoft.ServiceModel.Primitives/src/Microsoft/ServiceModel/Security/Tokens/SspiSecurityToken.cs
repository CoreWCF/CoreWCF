using Microsoft.IdentityModel;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Principal;
using System.Text;

namespace Microsoft.ServiceModel.Security.Tokens
{
    public class SspiSecurityToken : SecurityToken
    {
        string _id;
        DateTime _effectiveTime;
        DateTime _expirationTime;

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
