﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class UserNameSecurityToken : SecurityToken
    {
        private readonly string _id;
        private readonly DateTime _effectiveTime;
        public UserNameSecurityToken(string userName, string password)
            : this(userName, password, SecurityUniqueId.Create().Value)
        {
        }

        public UserNameSecurityToken(string userName, string password, string id)
        {
            if (userName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(userName));
            }

            if (userName == string.Empty)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.UserNameCannotBeEmpty);
            }

            UserName = userName;
            Password = password;
            _id = id;
            _effectiveTime = DateTime.UtcNow;
        }

        public override string Id => _id;

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => EmptyReadOnlyCollection<SecurityKey>.Instance;

        public override DateTime ValidFrom => _effectiveTime;

        public override DateTime ValidTo => SecurityUtils.MaxUtcDateTime;

        public string UserName { get; }

        public string Password { get; }
    }
}
