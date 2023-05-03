// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

public sealed class KafkaTransportSecurity
{
    private KafkaSecurityMode _mode = KafkaSecurityMode.None;
    private KafkaCredentialType _credentialType = KafkaCredentialType.None;

    public KafkaTransportSecurity()
    {

    }

    public KafkaTransportSecurity(KafkaSecurityMode mode)
    {
        _mode = mode;
    }

    public KafkaSecurityMode Mode
    {
        get => _mode;
        set
        {
            if (!KafkaSecurityModeHelper.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _mode = value;
        }
    }

    public KafkaCredentialType CredentialType
    {
        get => _credentialType;
        set
        {
            if (!KafkaCredentialTypeHelper.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (!KafkaCredentialTypeHelper.IsSupported(value))
            {
                throw new ArgumentException(SR.InvalidCredentialType);
            }

            _credentialType = value;
        }
    }

    internal void Apply(KafkaTransportBindingElement bindingElement)
    {
        if (_mode == KafkaSecurityMode.Transport)
        {
            if (_credentialType == KafkaCredentialType.None)
            {
                bindingElement.SecurityProtocol ??= SecurityProtocol.Ssl;
                bindingElement.SslCaPem ??= CaPem;
            }

            if (_credentialType == KafkaCredentialType.SslKeyPairCertificate)
            {
                if (SslKeyPairCredential is null)
                {
                    throw new NotSupportedException(SR.MissingSslKeyPairCredential);
                }

                bindingElement.SecurityProtocol ??= SecurityProtocol.Ssl;
                bindingElement.SslCaPem ??= CaPem;
                bindingElement.SslKeyPem ??= SslKeyPairCredential.SslKeyPem;
                bindingElement.SslKeyPassword ??= SslKeyPairCredential.SslKeyPassword;
                bindingElement.SslCertificatePem ??= SslKeyPairCredential.SslCertificatePem;

            }

            if (_credentialType == KafkaCredentialType.SaslPlain)
            {
                if (SaslUsernamePasswordCredential is null)
                {
                    throw new NotSupportedException(SR.MissingSaslUsernamePasswordCredential);
                }

                bindingElement.SslCaPem ??= CaPem;
                bindingElement.SaslMechanism ??= SaslMechanism.Plain;
                bindingElement.SecurityProtocol ??= SecurityProtocol.SaslSsl;
                bindingElement.SaslUsername ??= SaslUsernamePasswordCredential.SaslUsername;
                bindingElement.SaslPassword ??= SaslUsernamePasswordCredential.SaslPassword;
            }

            // TODO maps other security mechanism once requested. (Gssapi, Scram, OAuth..)
        }

        if (_mode == KafkaSecurityMode.TransportCredentialOnly)
        {
            if (_credentialType == KafkaCredentialType.SaslPlain)
            {
                if (SaslUsernamePasswordCredential is null)
                {
                    throw new NotSupportedException(SR.MissingSaslUsernamePasswordCredential);
                }

                bindingElement.SecurityProtocol ??= SecurityProtocol.SaslPlaintext;
                bindingElement.SaslMechanism ??= SaslMechanism.Plain;
                bindingElement.SaslUsername ??= SaslUsernamePasswordCredential.SaslUsername;
                bindingElement.SaslPassword ??= SaslUsernamePasswordCredential.SaslPassword;
            }

            // TODO maps other security mechanism once requested. (Gssapi, Scram, OAuth..)
        }

        if (_mode == KafkaSecurityMode.None)
        {
            bindingElement.SecurityProtocol ??= SecurityProtocol.Plaintext;
        }
    }

    public string CaPem { get; set; }

    public SaslUsernamePasswordCredential SaslUsernamePasswordCredential { get; set; }

    public SslKeyPairCredential SslKeyPairCredential { get; set; }
}

public class SaslUsernamePasswordCredential
{
    public SaslUsernamePasswordCredential(string saslUsername, string saslPassword)
    {
        SaslUsername = saslUsername;
        SaslPassword = saslPassword;
    }

    public string SaslUsername { get; }
    public string SaslPassword { get; }
}

public class SslKeyPairCredential
{
    public string SslCertificatePem { get; set; }
    public string SslKeyPem { get; set; }
    public string SslKeyPassword { get; set; }
}
