// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    internal sealed class DerivedKeySecurityToken : SecurityToken
    {
        private static readonly byte[] s_defaultLabel = new byte[]
            {
                (byte)'W', (byte)'S', (byte)'-', (byte)'S', (byte)'e', (byte)'c', (byte)'u', (byte)'r', (byte)'e',
                (byte)'C', (byte)'o', (byte)'n', (byte)'v', (byte)'e', (byte)'r', (byte)'s', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n',
                (byte)'W', (byte)'S', (byte)'-', (byte)'S', (byte)'e', (byte)'c', (byte)'u', (byte)'r', (byte)'e',
                (byte)'C', (byte)'o', (byte)'n', (byte)'v', (byte)'e', (byte)'r', (byte)'s', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n'
            };

        public const int DefaultNonceLength = 16;
        public const int DefaultDerivedKeyLength = 32;
        private string _id;
        private byte[] _key;
        private ReadOnlyCollection<SecurityKey> _securityKeys;

        // create from scratch
        public DerivedKeySecurityToken(SecurityToken tokenToDerive, SecurityKeyIdentifierClause tokenToDeriveIdentifier, int length)
            : this(tokenToDerive, tokenToDeriveIdentifier, length, SecurityUtils.GenerateId())
        {
        }

        internal DerivedKeySecurityToken(SecurityToken tokenToDerive, SecurityKeyIdentifierClause tokenToDeriveIdentifier,
            int length, string id)
        {
            if (length != 16 && length != 24 && length != 32)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.Psha1KeyLengthInvalid, length * 8)));
            }

            byte[] nonce = new byte[DefaultNonceLength];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetBytes(nonce);

            Initialize(id, -1, 0, length, null, nonce, tokenToDerive, tokenToDeriveIdentifier, SecurityAlgorithms.Psha1KeyDerivation);
        }

        internal DerivedKeySecurityToken(int generation, int offset, int length,
            string label, int minNonceLength, SecurityToken tokenToDerive,
            SecurityKeyIdentifierClause tokenToDeriveIdentifier,
            string derivationAlgorithm, string id)
        {
            byte[] nonce = new byte[minNonceLength];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetBytes(nonce);

            Initialize(id, generation, offset, length, label, nonce, tokenToDerive, tokenToDeriveIdentifier, derivationAlgorithm);
        }

        // create from xml
        internal DerivedKeySecurityToken(int generation, int offset, int length,
            string label, byte[] nonce, SecurityToken tokenToDerive,
            SecurityKeyIdentifierClause tokenToDeriveIdentifier, string derivationAlgorithm, string id)
        {
            Initialize(id, generation, offset, length, label, nonce, tokenToDerive, tokenToDeriveIdentifier, derivationAlgorithm, false);
        }

        public override string Id => _id;

        public override DateTime ValidFrom => TokenToDerive.ValidFrom;

        public override DateTime ValidTo => TokenToDerive.ValidTo;

        public string KeyDerivationAlgorithm { get; private set; }

        public int Generation { get; private set; } = -1;

        public string Label { get; private set; }

        public int Length { get; private set; } = -1;

        internal byte[] Nonce { get; private set; }

        public int Offset { get; private set; } = -1;

        internal SecurityToken TokenToDerive { get; private set; }

        internal SecurityKeyIdentifierClause TokenToDeriveIdentifier { get; private set; }

        public override ReadOnlyCollection<SecurityKey> SecurityKeys
        {
            get
            {
                if (_securityKeys == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.DerivedKeyNotInitialized));
                }
                return _securityKeys;
            }
        }

        public byte[] GetKeyBytes()
        {
            return SecurityUtils.CloneBuffer(_key);
        }

        public byte[] GetNonce()
        {
            return SecurityUtils.CloneBuffer(Nonce);
        }

        internal bool TryGetSecurityKeys(out ReadOnlyCollection<SecurityKey> keys)
        {
            keys = _securityKeys;
            return (keys != null);
        }

        public override string ToString()
        {
            StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);
            writer.WriteLine("DerivedKeySecurityToken:");
            writer.WriteLine("   Generation: {0}", Generation);
            writer.WriteLine("   Offset: {0}", Offset);
            writer.WriteLine("   Length: {0}", Length);
            writer.WriteLine("   Label: {0}", Label);
            writer.WriteLine("   Nonce: {0}", Convert.ToBase64String(Nonce));
            writer.WriteLine("   TokenToDeriveFrom:");
            using (XmlTextWriter xmlWriter = new XmlTextWriter(writer))
            {
                xmlWriter.Formatting = Formatting.Indented;
                SecurityStandardsManager.DefaultInstance.SecurityTokenSerializer.WriteKeyIdentifierClause(XmlDictionaryWriter.CreateDictionaryWriter(xmlWriter), TokenToDeriveIdentifier);
            }
            return writer.ToString();
        }

        private void Initialize(string id, int generation, int offset, int length, string label, byte[] nonce,
            SecurityToken tokenToDerive, SecurityKeyIdentifierClause tokenToDeriveIdentifier, string derivationAlgorithm)
        {
            Initialize(id, generation, offset, length, label, nonce, tokenToDerive, tokenToDeriveIdentifier, derivationAlgorithm, true);
        }

        private void Initialize(string id, int generation, int offset, int length, string label, byte[] nonce,
            SecurityToken tokenToDerive, SecurityKeyIdentifierClause tokenToDeriveIdentifier, string derivationAlgorithm,
            bool initializeDerivedKey)
        {
            if (tokenToDerive == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenToDerive));
            }

            if (!SecurityUtils.IsSupportedAlgorithm(derivationAlgorithm, tokenToDerive))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.DerivedKeyCannotDeriveFromSecret));
            }

            if (length == -1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(length)));
            }
            if (offset == -1 && generation == -1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.DerivedKeyPosAndGenNotSpecified);
            }
            if (offset >= 0 && generation >= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.DerivedKeyPosAndGenBothSpecified);
            }

            _id = id ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            Label = label;
            Nonce = nonce ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(nonce));
            Length = length;
            Offset = offset;
            Generation = generation;
            TokenToDerive = tokenToDerive;
            TokenToDeriveIdentifier = tokenToDeriveIdentifier ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenToDeriveIdentifier));
            KeyDerivationAlgorithm = derivationAlgorithm;

            if (initializeDerivedKey)
            {
                InitializeDerivedKey(Length);
            }
        }

        internal void InitializeDerivedKey(int maxKeyLength)
        {
            if (_key != null)
            {
                return;
            }
            if (Length > maxKeyLength)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.DerivedKeyLengthTooLong, Length, maxKeyLength));
            }

            _key = SecurityUtils.GenerateDerivedKey(TokenToDerive, KeyDerivationAlgorithm,
                (Label != null ? Encoding.UTF8.GetBytes(Label) : s_defaultLabel), Nonce, Length * 8,
                ((Offset >= 0) ? Offset : Generation * Length));
            if ((_key == null) || (_key.Length == 0))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.DerivedKeyCannotDeriveFromSecret);
            }
            List<SecurityKey> temp = new List<SecurityKey>(1)
            {
                new InMemorySymmetricSecurityKey(_key, false)
            };
            _securityKeys = temp.AsReadOnly();
        }

        internal void InitializeDerivedKey(ReadOnlyCollection<SecurityKey> securityKeys)
        {
            _key = ((SymmetricSecurityKey)securityKeys[0]).GetSymmetricKey();
            _securityKeys = securityKeys;
        }

        internal static void EnsureAcceptableOffset(int offset, int generation, int length, int maxOffset)
        {
            if (offset != -1)
            {
                if (offset > maxOffset)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.DerivedKeyTokenOffsetTooHigh, offset, maxOffset)));
                }
            }
            else
            {
                int effectiveOffset = generation * length;
                if ((effectiveOffset < generation && effectiveOffset < length) || effectiveOffset > maxOffset)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(SR.DerivedKeyTokenGenerationAndLengthTooHigh, generation, length, maxOffset)));
                }
            }
        }
    }
}
