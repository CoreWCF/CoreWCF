using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CoreWCF.Dispatcher
{
    internal static class ExceptionHandlerHelper
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
