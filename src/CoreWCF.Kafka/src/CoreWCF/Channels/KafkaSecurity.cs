// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using CoreWCF.Runtime;

namespace CoreWCF.Channels;

public sealed class KafkaSecurity
{
    internal const KafkaSecurityMode DefaultMode = KafkaSecurityMode.None;
    private KafkaSecurityMode _mode;
    private KafkaTransportSecurity _transportSecurity;
    private KafkaMessageSecurity _messageSecurity;

    public KafkaSecurity()
        : this(DefaultMode, new KafkaTransportSecurity(), new KafkaMessageSecurity())
    {
    }

    private KafkaSecurity(KafkaSecurityMode mode, KafkaTransportSecurity transportSecurity, KafkaMessageSecurity messageSecurity)
    {
        Fx.Assert(KafkaSecurityModeHelper.IsDefined(mode), $"Invalid SecurityMode value: {mode.ToString()}.");

        if (!KafkaSecurityModeHelper.IsSupported(mode))
        {
            throw new NotSupportedException(SR.SecurityModeNotSupportedYet);
        }

        _mode = mode;
        _transportSecurity = transportSecurity ?? new KafkaTransportSecurity();
        _messageSecurity = messageSecurity ?? new KafkaMessageSecurity();
    }

    [DefaultValue(DefaultMode)]
    public KafkaSecurityMode Mode
    {
        get { return _mode; }
        set
        {
            if (!KafkaSecurityModeHelper.IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
            }

            if (!KafkaSecurityModeHelper.IsSupported(value))
            {
                throw new NotSupportedException(SR.SecurityModeNotSupportedYet);
            }

            _mode = value;
        }
    }

    public KafkaTransportSecurity Transport
    {
        get => _transportSecurity;
        set => _transportSecurity = value ?? throw new ArgumentNullException(nameof(value));
    }

    public KafkaMessageSecurity Message
    {
        get => _messageSecurity;
        set => _messageSecurity = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal SecurityBindingElement CreateMessageSecurity()
    {
        return null;
    }

    internal void ApplySecurity(KafkaTransportBindingElement element)
    {
        if (_mode == KafkaSecurityMode.Transport || _mode == KafkaSecurityMode.TransportWithMessageCredential)
        {
            _transportSecurity.ConfigureTransportSecurityWithClientAuthentication(element);
        }
        else if (_mode == KafkaSecurityMode.TransportCredentialOnly)
        {
            _transportSecurity.ConfigureClientAuthenticationOnly(element);
        }
        else if (_mode == KafkaSecurityMode.None || _mode == KafkaSecurityMode.Message)
        {
            _transportSecurity.ConfigureNoTransportSecurity(element);
        }
    }
}
