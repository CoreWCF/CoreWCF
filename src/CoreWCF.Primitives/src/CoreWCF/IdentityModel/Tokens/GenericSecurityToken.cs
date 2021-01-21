﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class GenericSecurityToken : SecurityToken
    {
        private readonly string _id;
        private readonly DateTime _effectiveTime;
        private readonly DateTime _expirationTime;

        internal GenericSecurityToken(string name, string id)
        {
            Name = name ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            _id = id ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            _effectiveTime = DateTime.UtcNow;
            _expirationTime = DateTime.UtcNow.AddHours(10);
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
    }
}

