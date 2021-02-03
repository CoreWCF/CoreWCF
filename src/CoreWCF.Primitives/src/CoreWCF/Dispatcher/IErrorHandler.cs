// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public interface IErrorHandler
    {
        void ProvideFault(Exception error, MessageVersion version, ref Message fault);
        bool HandleError(Exception error);
    }
}