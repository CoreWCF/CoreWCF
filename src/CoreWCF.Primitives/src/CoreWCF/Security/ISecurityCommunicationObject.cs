// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Security
{
    internal interface ISecurityCommunicationObject
    {
        TimeSpan DefaultOpenTimeout { get; }
        TimeSpan DefaultCloseTimeout { get; }
        void OnAbort();
        Task OnCloseAsync(CancellationToken token);
        void OnClosed();
        void OnClosing();
        void OnFaulted();
        Task OnOpenAsync(CancellationToken token);
        void OnOpened();
        void OnOpening();
    }
}
