// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.IdentityModel.Tokens.Saml
{
    public class BinarySecret
    {
        public BinarySecret(string secret)
        {
            Value = secret;
        }

        public string Value { get; }

        public byte[] GetBytes()
        {
            if (Value == null)
            {
                return null;
            }

            return Convert.FromBase64String(Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is not BinarySecret other)
                return false;
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(Value, other.Value);
        }

        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }
    }
}
