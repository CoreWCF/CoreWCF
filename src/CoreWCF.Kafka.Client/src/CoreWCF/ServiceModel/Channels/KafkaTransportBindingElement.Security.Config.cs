// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

public partial class KafkaTransportBindingElement
{
    /// <summary>
    /// <inheritdoc cref="ClientConfig.SecurityProtocol"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public SecurityProtocol? SecurityProtocol
    {
        get => Config.SecurityProtocol;
        set
        {
            if (value.HasValue && !SecurityProtocolHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.SecurityProtocol = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslMechanism"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public SaslMechanism? SaslMechanism
    {
        get => Config.SaslMechanism;
        set
        {
            if (value.HasValue && !SaslMechanismHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.SaslMechanism = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslCaLocation"/>
    /// </summary>
    public string SslCaLocation
    {
        get => Config.SslCaLocation;
        set => Config.SslCaLocation = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslCaPem"/>
    /// </summary>
    public string SslCaPem
    {
        get => Config.SslCaPem;
        set => Config.SslCaPem = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslUsername"/>
    /// </summary>
    public string SaslUsername
    {
        get => Config.SaslUsername;
        set => Config.SaslUsername = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslPassword"/>
    /// </summary>
    public string SaslPassword
    {
        get => Config.SaslPassword;
        set => Config.SaslPassword = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslKeyPem"/>
    /// </summary>
    public string SslKeyPem
    {
        get => Config.SslKeyPem;
        set => Config.SslKeyPem = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslKeyPassword"/>
    /// </summary>
    public string SslKeyPassword
    {
        get => Config.SslKeyPassword;
        set => Config.SslKeyPassword = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslKeyLocation"/>
    /// </summary>
    public string SslKeyLocation
    {
        get => Config.SslKeyLocation;
        set => Config.SslKeyLocation = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslKeystorePassword"/>
    /// </summary>
    public string SslKeystorePassword
    {
        get => Config.SslKeystorePassword;
        set => Config.SslKeystorePassword = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslKeystoreLocation"/>
    /// </summary>
    public string SslKeystoreLocation
    {
        get => Config.SslKeystoreLocation;
        set => Config.SslKeystoreLocation = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslCertificateLocation"/>
    /// </summary>
    public string SslCertificateLocation
    {
        get => Config.SslCertificateLocation;
        set => Config.SslCertificateLocation = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslCertificatePem"/>
    /// </summary>
    public string SslCertificatePem
    {
        get => Config.SslCertificatePem;
        set => Config.SslCertificatePem = value;
    }






    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslCipherSuites"/>
    /// </summary>
    public string SslCipherSuites
    {
        get => Config.SslCipherSuites;
        set => Config.SslCipherSuites = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslCurvesList"/>
    /// </summary>
    public string SslCurvesList
    {
        get => Config.SslCurvesList;
        set => Config.SslCurvesList = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslSigalgsList"/>
    /// </summary>
    public string SslSigalgsList
    {
        get => Config.SslSigalgsList;
        set => Config.SslSigalgsList = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslCaCertificateStores"/>
    /// </summary>
    public string SslCaCertificateStores
    {
        get => Config.SslCaCertificateStores;
        set => Config.SslCaCertificateStores = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslCrlLocation"/>
    /// </summary>
    public string SslCrlLocation
    {
        get => Config.SslCrlLocation;
        set => Config.SslCrlLocation = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslProviders"/>
    /// </summary>
    public string SslProviders
    {
        get => Config.SslProviders;
        set => Config.SslProviders = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslEngineLocation"/>
    /// </summary>
    public string SslEngineLocation
    {
        get => Config.SslEngineLocation;
        set => Config.SslEngineLocation = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslEngineId"/>
    /// </summary>
    public string SslEngineId
    {
        get => Config.SslEngineId;
        set => Config.SslEngineId = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.EnableSslCertificateVerification"/>
    /// </summary>
    public bool? EnableSslCertificateVerification
    {
        get => Config.EnableSslCertificateVerification;
        set => Config.EnableSslCertificateVerification = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SslEndpointIdentificationAlgorithm"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public SslEndpointIdentificationAlgorithm? SslEndpointIdentificationAlgorithm
    {
        get => Config.SslEndpointIdentificationAlgorithm;
        set
        {
            if (value.HasValue && !SslEndpointIdentificationAlgorithmHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.SslEndpointIdentificationAlgorithm = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslKerberosServiceName"/>
    /// </summary>
    public string SaslKerberosServiceName
    {
        get => Config.SaslKerberosServiceName;
        set => Config.SaslKerberosServiceName = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslKerberosPrincipal"/>
    /// </summary>
    public string SaslKerberosPrincipal
    {
        get => Config.SaslKerberosPrincipal;
        set => Config.SaslKerberosPrincipal = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslKerberosKinitCmd"/>
    /// </summary>
    public string SaslKerberosKinitCmd
    {
        get => Config.SaslKerberosKinitCmd;
        set => Config.SaslKerberosKinitCmd = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslKerberosKeytab"/>
    /// </summary>
    public string SaslKerberosKeytab
    {
        get => Config.SaslKerberosKeytab;
        set => Config.SaslKerberosKeytab = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslKerberosMinTimeBeforeRelogin"/>
    /// </summary>
    public int? SaslKerberosMinTimeBeforeRelogin
    {
        get => Config.SaslKerberosMinTimeBeforeRelogin;
        set => Config.SaslKerberosMinTimeBeforeRelogin = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslOauthbearerConfig"/>
    /// </summary>
    public string SaslOauthbearerConfig
    {
        get => Config.SaslOauthbearerConfig;
        set => Config.SaslOauthbearerConfig = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.EnableSaslOauthbearerUnsecureJwt"/>
    /// </summary>
    public bool? EnableSaslOauthbearerUnsecureJwt
    {
        get => Config.EnableSaslOauthbearerUnsecureJwt;
        set => Config.EnableSaslOauthbearerUnsecureJwt = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslOauthbearerMethod"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public SaslOauthbearerMethod? SaslOauthbearerMethod
    {
        get => Config.SaslOauthbearerMethod;
        set
        {
            if (value.HasValue && !SaslOauthbearerMethodHelper.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Config.SaslOauthbearerMethod = value;
        }
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslOauthbearerClientId"/>
    /// </summary>
    public string SaslOauthbearerClientId
    {
        get => Config.SaslOauthbearerClientId;
        set => Config.SaslOauthbearerClientId = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslOauthbearerClientSecret"/>
    /// </summary>
    public string SaslOauthbearerClientSecret
    {
        get => Config.SaslOauthbearerClientSecret;
        set => Config.SaslOauthbearerClientSecret = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslOauthbearerScope"/>
    /// </summary>
    public string SaslOauthbearerScope
    {
        get => Config.SaslOauthbearerScope;
        set => Config.SaslOauthbearerScope = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslOauthbearerExtensions"/>
    /// </summary>
    public string SaslOauthbearerExtensions
    {
        get => Config.SaslOauthbearerExtensions;
        set => Config.SaslOauthbearerExtensions = value;
    }

    /// <summary>
    /// <inheritdoc cref="ClientConfig.SaslOauthbearerTokenEndpointUrl"/>
    /// </summary>
    public string SaslOauthbearerTokenEndpointUrl
    {
        get => Config.SaslOauthbearerTokenEndpointUrl;
        set => Config.SaslOauthbearerTokenEndpointUrl = value;
    }
}
