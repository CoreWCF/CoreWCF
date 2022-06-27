// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Security.Principal;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    public class GenericIdentitySecurityToken : SecurityToken
    {
        private readonly string _id;
        private readonly DateTime _effectiveTime;
        private readonly DateTime _expirationTime;
        private readonly GenericIdentity _genericIdentity;

        public GenericIdentitySecurityToken(GenericIdentity genericIdentity)
            : this(genericIdentity, SecurityUniqueId.Create().Value)
        {
        }

        public GenericIdentitySecurityToken(GenericIdentity genericIdentity, string id)
        {
            Name = genericIdentity.Name;
            _id = id ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            _effectiveTime = DateTime.UtcNow;
            _expirationTime = SecurityUtils.MaxUtcDateTime;
            _genericIdentity = (GenericIdentity) genericIdentity.Clone();
        }

        public override string Id
        {
            get { return _id; }
        }

        public override ReadOnlyCollection<SecurityKey> SecurityKeys
        {
            get { return EmptyReadOnlyCollection<SecurityKey>.Instance; }
        }

        public override DateTime ValidFrom
        {
            get { return _effectiveTime; }
        }

        public override DateTime ValidTo
        {
            get { return _expirationTime; }
        }

        public string Name { get; }

        public virtual GenericIdentity GenericIdentity
        {
            get
            {
                return _genericIdentity;
            }
        }
    }
}

