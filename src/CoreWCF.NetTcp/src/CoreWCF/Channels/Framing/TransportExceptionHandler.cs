// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Channels.Framing
{
    internal class TransportExceptionHandler
    {
        internal static bool HandleTransportExceptionHelper(Exception exception)
        {
            if (exception == null)
            {
                throw Fx.AssertAndThrow("Null exception passed to HandleTransportExceptionHelper.");
            }

            ExceptionHandler handler = ExceptionHandler.TransportExceptionHandler;
            if (handler == null)
            {
                return false;
            }

            try
            {
                if (!handler.HandleException(exception))
                {
                    return false;
                }
            }
            catch (Exception thrownException)
            {
                if (Fx.IsFatal(thrownException))
                {
                    throw;
                }

                DiagnosticUtility.TraceHandledException(thrownException, TraceEventType.Error);
                return false;
            }

            DiagnosticUtility.TraceHandledException(exception, TraceEventType.Error);
            return true;
        }
    }
}
