// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Net.Security;

namespace CoreWCF.Channels
{
    public sealed class MsmqTransportSecurity
    {
        private MsmqAuthenticationMode _msmqAuthenticationMode;
        private MsmqEncryptionAlgorithm _msmqEncryptionAlgorithm;
        private MsmqSecureHashAlgorithm _msmqHashAlgorithm;
        private ProtectionLevel _msmqProtectionLevel;

        public MsmqTransportSecurity()
        {
            _msmqAuthenticationMode = MsmqDefaults.MsmqAuthenticationMode1;
            _msmqEncryptionAlgorithm = MsmqDefaults.MsmqEncryptionAlgorithm1;
            _msmqHashAlgorithm = MsmqDefaults.MsmqSecureHashAlgorithm;
            _msmqProtectionLevel = MsmqDefaults.MsmqProtectionLevel;
        }

        public MsmqTransportSecurity(MsmqTransportSecurity other)
        {
            if (null == other)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(other));
            _msmqAuthenticationMode = other.MsmqAuthenticationMode;
            _msmqEncryptionAlgorithm = other.MsmqEncryptionAlgorithm;
            _msmqHashAlgorithm = other.MsmqSecureHashAlgorithm;
            _msmqProtectionLevel = other.MsmqProtectionLevel;
        }

        internal bool Enabled
        {
            get
            {
                return _msmqAuthenticationMode != MsmqAuthenticationMode.None && _msmqProtectionLevel != ProtectionLevel.None;
            }
        }

        [DefaultValue(MsmqDefaults.MsmqAuthenticationMode1)]
        public MsmqAuthenticationMode MsmqAuthenticationMode
        {
            get { return _msmqAuthenticationMode; }
            set
            {
                //if (!MsmqAuthenticationModeHelper.IsDefined(value))
                //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                _msmqAuthenticationMode = value;
            }
        }

        [DefaultValue(MsmqDefaults.MsmqEncryptionAlgorithm1)]
        public MsmqEncryptionAlgorithm MsmqEncryptionAlgorithm
        {
            get { return _msmqEncryptionAlgorithm; }
            set
            {
                if (!MsmqEncryptionAlgorithmHelper.IsDefined(value))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                _msmqEncryptionAlgorithm = value;
            }
        }

        [DefaultValue(MsmqDefaults.DefaultMsmqSecureHashAlgorithm)]
        public MsmqSecureHashAlgorithm MsmqSecureHashAlgorithm
        {
            get { return _msmqHashAlgorithm; }
            set
            {
                //if (!MsmqSecureHashAlgorithmHelper.IsDefined(value))
                //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                _msmqHashAlgorithm = value;
            }
        }

        [DefaultValue(MsmqDefaults.MsmqProtectionLevel)]
        public ProtectionLevel MsmqProtectionLevel
        {
            get { return _msmqProtectionLevel; }
            set
            {
                //if (!ProtectionLevelHelper.IsDefined(value))
                //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                _msmqProtectionLevel = value;
            }
        }

        internal void Disable()
        {
            _msmqAuthenticationMode = MsmqAuthenticationMode.None;
            _msmqProtectionLevel = ProtectionLevel.None;
        }
    }
}
