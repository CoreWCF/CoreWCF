using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace CoreWCF.Security.Tokens
{
    internal sealed class DerivedKeySecurityToken : SecurityToken
    {
        private static readonly byte[] DefaultLabel = new byte[]
            {
                (byte)'W', (byte)'S', (byte)'-', (byte)'S', (byte)'e', (byte)'c', (byte)'u', (byte)'r', (byte)'e',
                (byte)'C', (byte)'o', (byte)'n', (byte)'v', (byte)'e', (byte)'r', (byte)'s', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n',
                (byte)'W', (byte)'S', (byte)'-', (byte)'S', (byte)'e', (byte)'c', (byte)'u', (byte)'r', (byte)'e',
                (byte)'C', (byte)'o', (byte)'n', (byte)'v', (byte)'e', (byte)'r', (byte)'s', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n'
            };

        public const int DefaultNonceLength = 16;
        public const int DefaultDerivedKeyLength = 32;
        private string id;
        private byte[] key;
        private string keyDerivationAlgorithm;
        private string label;
        private int length = -1;
        private byte[] nonce;

        // either offset or generation must be specified.
        private int offset = -1;
        private int generation = -1;
        private SecurityToken tokenToDerive;
        private SecurityKeyIdentifierClause tokenToDeriveIdentifier;
        private ReadOnlyCollection<SecurityKey> securityKeys;

        // create from scratch
        public DerivedKeySecurityToken(SecurityToken tokenToDerive, SecurityKeyIdentifierClause tokenToDeriveIdentifier, int length)
            : this(tokenToDerive, tokenToDeriveIdentifier, length, SecurityUtils.GenerateId())
        {
        }

        internal DerivedKeySecurityToken(SecurityToken tokenToDerive, SecurityKeyIdentifierClause tokenToDeriveIdentifier,
            int length, string id)
        {
            if (length != 16 && length != 24 && length != 32)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.Psha1KeyLengthInvalid, length * 8)));

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

        public override string Id => this.id;

        public override DateTime ValidFrom => this.tokenToDerive.ValidFrom;

        public override DateTime ValidTo => this.tokenToDerive.ValidTo;

        public string KeyDerivationAlgorithm => keyDerivationAlgorithm;

        public int Generation => this.generation;

        public string Label => this.label;

        public int Length => this.length;

        internal byte[] Nonce => this.nonce;

        public int Offset => this.offset;

        internal SecurityToken TokenToDerive => this.tokenToDerive;

        internal SecurityKeyIdentifierClause TokenToDeriveIdentifier => this.tokenToDeriveIdentifier;

        public override ReadOnlyCollection<SecurityKey> SecurityKeys
        {
            get
            {
                if (this.securityKeys == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.DerivedKeyNotInitialized));
                }
                return this.securityKeys;
            }
        }

        public byte[] GetKeyBytes()
        {
            return SecurityUtils.CloneBuffer(this.key);
        }

        public byte[] GetNonce()
        {
            return SecurityUtils.CloneBuffer(this.nonce);
        }

        internal bool TryGetSecurityKeys(out ReadOnlyCollection<SecurityKey> keys)
        {
            keys = this.securityKeys;
            return (keys != null);
        }

        public override string ToString()
        {
            StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);
            writer.WriteLine("DerivedKeySecurityToken:");
            writer.WriteLine("   Generation: {0}", this.Generation);
            writer.WriteLine("   Offset: {0}", this.Offset);
            writer.WriteLine("   Length: {0}", this.Length);
            writer.WriteLine("   Label: {0}", this.Label);
            writer.WriteLine("   Nonce: {0}", Convert.ToBase64String(this.Nonce));
            writer.WriteLine("   TokenToDeriveFrom:");
            using (XmlTextWriter xmlWriter = new XmlTextWriter(writer))
            {
                xmlWriter.Formatting = Formatting.Indented;
                SecurityStandardsManager.DefaultInstance.SecurityTokenSerializer.WriteKeyIdentifierClause(XmlDictionaryWriter.CreateDictionaryWriter(xmlWriter), this.TokenToDeriveIdentifier);
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
            if (id == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            }
            if (tokenToDerive == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenToDerive));
            }
            if (tokenToDeriveIdentifier == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenToDeriveIdentifier));
            }

            if (!SecurityUtils.IsSupportedAlgorithm(derivationAlgorithm, tokenToDerive))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.DerivedKeyCannotDeriveFromSecret));
            }
            if (nonce == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(nonce));
            }
            if (length == -1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("length"));
            }
            if (offset == -1 && generation == -1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.DerivedKeyPosAndGenNotSpecified);
            }
            if (offset >= 0 && generation >= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.DerivedKeyPosAndGenBothSpecified);
            }

            this.id = id;
            this.label = label;
            this.nonce = nonce;
            this.length = length;
            this.offset = offset;
            this.generation = generation;
            this.tokenToDerive = tokenToDerive;
            this.tokenToDeriveIdentifier = tokenToDeriveIdentifier;
            this.keyDerivationAlgorithm = derivationAlgorithm;

            if (initializeDerivedKey)
            {
                InitializeDerivedKey(this.length);
            }
        }

        internal void InitializeDerivedKey(int maxKeyLength)
        {
            if (this.key != null)
            {
                return;
            }
            if (this.length > maxKeyLength)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.DerivedKeyLengthTooLong, this.length, maxKeyLength));
            }

            this.key = SecurityUtils.GenerateDerivedKey(this.tokenToDerive, this.keyDerivationAlgorithm,
                (this.label != null ? Encoding.UTF8.GetBytes(this.label) : DefaultLabel), this.nonce, this.length * 8,
                ((this.offset >= 0) ? this.offset : this.generation * this.length));
            if ((this.key == null) || (this.key.Length == 0))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.DerivedKeyCannotDeriveFromSecret);
            }
            List<SecurityKey> temp = new List<SecurityKey>(1);
            temp.Add(new InMemorySymmetricSecurityKey(this.key, false));
            this.securityKeys = temp.AsReadOnly();
        }

        internal void InitializeDerivedKey(ReadOnlyCollection<SecurityKey> securityKeys)
        {
            this.key = ((SymmetricSecurityKey)securityKeys[0]).GetSymmetricKey();
            this.securityKeys = securityKeys;
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
