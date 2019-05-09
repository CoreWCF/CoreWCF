using Microsoft.ServiceModel.Channels;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal static class ErrorBehaviorHelper
    {
        // This ensures that people debugging first-chance Exceptions see this Exception,
        // and that the Exception shows up in the trace logs.
        internal static void ThrowAndCatch(Exception e, Message message)
        {
            try
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    if (message == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(e);
                    }
                    else
                    {
                        throw TraceUtility.ThrowHelperError(e, message);
                    }
                }
                else
                {
                    if (message == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(e);
                    }
                    else
                    {
                        TraceUtility.ThrowHelperError(e, message);
                    }
                }
            }
            catch (Exception e2)
            {
                if (!object.ReferenceEquals(e, e2))
                {
                    throw;
                }
            }
        }

        internal static void ThrowAndCatch(Exception e)
        {
            ThrowAndCatch(e, null);
        }
    }
}
