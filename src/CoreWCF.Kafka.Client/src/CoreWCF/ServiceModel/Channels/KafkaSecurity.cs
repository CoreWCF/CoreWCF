// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.ServiceModel.Channels;

namespace CoreWCF.ServiceModel.Channels;

public sealed class KafkaSecurity
{
    private KafkaTransportSecurity _transportSecurity;
    private KafkaMessageSecurity _messageSecurity;
    private KafkaSecurityMode _mode;
    internal const KafkaSecurityMode DefaultMode = KafkaSecurityMode.None;

    public KafkaSecurity()
        : this(DefaultMode, new KafkaTransportSecurity(), new KafkaMessageSecurity())
    {

    }

    private KafkaSecurity(KafkaSecurityMode mode, KafkaTransportSecurity transportSecurity,
        KafkaMessageSecurity messageSecurity)
    {
        _mode = mode;
        _transportSecurity = transportSecurity;
        _messageSecurity = messageSecurity;
    }

    [DefaultValue(DefaultMode)]
    public KafkaSecurityMode Mode
    {
        get { return _mode; }
        set
        {
            if (!KafkaSecurityModeHelper.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (!KafkaSecurityModeHelper.IsSupported(value))
            {
                throw new NotSupportedException("Security mode not supported yet.");
            }

            _mode = value;
        }
    }

    public KafkaTransportSecurity Transport
    {
        get => _transportSecurity;
        set => _transportSecurity = value ?? new KafkaTransportSecurity();
    }

    public KafkaMessageSecurity Message
    {
        get => _messageSecurity;
        set => _messageSecurity = value ?? new KafkaMessageSecurity();
    }

    internal SecurityBindingElement CreateMessageSecurity()
    {
        return null;
    }

    internal void ApplySecurity(KafkaTransportBindingElement bindingElement)
    {
        if (_mode == KafkaSecurityMode.Transport || _mode == KafkaSecurityMode.TransportWithMessageCredential)
        {
            _transportSecurity.ConfigureTransportSecurityWithClientAuthentication(bindingElement);
        }
        else if (_mode == KafkaSecurityMode.TransportCredentialOnly)
        {
            _transportSecurity.ConfigureClientAuthenticationOnly(bindingElement);
        }
        else if (_mode == KafkaSecurityMode.None || _mode == KafkaSecurityMode.Message)
        {
            _transportSecurity.ConfigureNoTransportSecurity(bindingElement);
        }
    }
}
