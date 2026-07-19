// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.IdentityModel.Tokens;
using Xunit;

namespace CoreWCF.Http.Tests.Security
{
    public class SamlValidateTokenInputValidationTests
    {
        [Fact]
        public void Saml_ValidateToken_NullToken_ThrowsArgumentNull()
        {
            CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler handler =
                new CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler
                {
                    Configuration = new SecurityTokenHandlerConfiguration()
                };

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => handler.ValidateToken(null));
            Assert.Equal("token", ex.ParamName);
        }

        [Fact]
        public void Saml_ValidateToken_WrongTokenType_ThrowsArgumentException()
        {
            CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler handler =
                new CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler
                {
                    Configuration = new SecurityTokenHandlerConfiguration()
                };

            SecurityToken wrongTypeToken = new UserNameSecurityToken("user", "pass");

            ArgumentException ex = Assert.Throws<ArgumentException>(() => handler.ValidateToken(wrongTypeToken));
            Assert.Equal("token", ex.ParamName);
            Assert.Contains(wrongTypeToken.GetType().ToString(), ex.Message);
        }

        [Fact]
        public void Saml_ValidateToken_NullConfiguration_ThrowsInvalidOperation()
        {
            CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler handler =
                new CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler();

            CoreWCF.IdentityModel.Tokens.SamlSecurityToken token = new SamlSecurityTokenStub();

            Assert.Throws<InvalidOperationException>(() => handler.ValidateToken(token));
        }

        [Fact]
        public void Saml_ValidateToken_NullAssertion_ThrowsArgumentException()
        {
            CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler handler =
                new CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler
                {
                    Configuration = new SecurityTokenHandlerConfiguration()
                };

            CoreWCF.IdentityModel.Tokens.SamlSecurityToken token = new SamlSecurityTokenStub();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => handler.ValidateToken(token));
            Assert.Equal("token", ex.ParamName);
        }

        [Fact]
        public void Saml2_ValidateToken_NullToken_ThrowsArgumentNull()
        {
            CoreWCF.IdentityModel.Tokens.Saml2SecurityTokenHandler handler =
                new CoreWCF.IdentityModel.Tokens.Saml2SecurityTokenHandler
                {
                    Configuration = new SecurityTokenHandlerConfiguration()
                };

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => handler.ValidateToken(null));
            Assert.Equal("token", ex.ParamName);
        }

        [Fact]
        public void Saml2_ValidateToken_WrongTokenType_ThrowsArgumentException()
        {
            CoreWCF.IdentityModel.Tokens.Saml2SecurityTokenHandler handler =
                new CoreWCF.IdentityModel.Tokens.Saml2SecurityTokenHandler
                {
                    Configuration = new SecurityTokenHandlerConfiguration()
                };

            SecurityToken wrongTypeToken = new UserNameSecurityToken("user", "pass");

            ArgumentException ex = Assert.Throws<ArgumentException>(() => handler.ValidateToken(wrongTypeToken));
            Assert.Equal("token", ex.ParamName);
        }

        [Fact]
        public void Saml2_ValidateToken_NullConfiguration_ThrowsInvalidOperation()
        {
            CoreWCF.IdentityModel.Tokens.Saml2SecurityTokenHandler handler =
                new CoreWCF.IdentityModel.Tokens.Saml2SecurityTokenHandler();

            CoreWCF.IdentityModel.Tokens.Saml2SecurityToken token = new Saml2SecurityTokenStub();

            Assert.Throws<InvalidOperationException>(() => handler.ValidateToken(token));
        }

        [Fact]
        public void Saml2_ValidateToken_NullAssertion_ThrowsArgumentException()
        {
            CoreWCF.IdentityModel.Tokens.Saml2SecurityTokenHandler handler =
                new CoreWCF.IdentityModel.Tokens.Saml2SecurityTokenHandler
                {
                    Configuration = new SecurityTokenHandlerConfiguration()
                };

            CoreWCF.IdentityModel.Tokens.Saml2SecurityToken token = new Saml2SecurityTokenStub();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => handler.ValidateToken(token));
            Assert.Equal("token", ex.ParamName);
        }

        private sealed class SamlSecurityTokenStub : CoreWCF.IdentityModel.Tokens.SamlSecurityToken
        {
            public SamlSecurityTokenStub()
            {
            }
        }

        private sealed class Saml2SecurityTokenStub : CoreWCF.IdentityModel.Tokens.Saml2SecurityToken
        {
            public Saml2SecurityTokenStub()
            {
            }
        }
    }
}
