// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.IdentityModel.Tokens;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class SecurityTokenTests
    {
        [Fact]
        public void CanCreateInMemorySymmetricSecurityKeysFromBase64Strings()
        {
            const string clientSecretBase64 = "MTIzNA==";
            const string secondaryClientSecretBase64 = "MTIzNDU2Nzg=";

            List<SecurityKey> securityKeys = CreateSymmetricSecurityKeys(new[] { clientSecretBase64, secondaryClientSecretBase64 });

            Assert.NotNull(securityKeys);
            Assert.Equal(2, securityKeys.Count);
            Assert.Equal(32, securityKeys[0].KeySize);
            Assert.Equal(64, securityKeys[1].KeySize);
        }

        private List<SecurityKey> CreateSymmetricSecurityKeys(IEnumerable<string> base64EncodedKeys)
        {
            var symmetricKeys = new List<SecurityKey>();
            foreach (string base64Key in base64EncodedKeys)
            {
                byte[] keyBytes = Convert.FromBase64String(base64Key);
                symmetricKeys.Add(new InMemorySymmetricSecurityKey(keyBytes));
            }

            return symmetricKeys;
        }
    }
}
