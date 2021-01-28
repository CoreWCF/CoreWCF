// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    public abstract class ExceptionHandler
    {
        private static ExceptionHandler s_transportExceptionHandler = AlwaysHandle;

        public static ExceptionHandler AlwaysHandle { get; } = new AlwaysHandleExceptionHandler();

        public static ExceptionHandler AsynchronousThreadExceptionHandler
        {
            get
            {
                HandlerWrapper wrapper = (HandlerWrapper)Fx.AsynchronousThreadExceptionHandler;
                return wrapper == null ? null : wrapper.Handler;
            }

            set
            {
                Fx.AsynchronousThreadExceptionHandler = value == null ? null : new HandlerWrapper(value);
            }
        }

        public static ExceptionHandler TransportExceptionHandler
        {
            get
            {
                return s_transportExceptionHandler;
            }

            set
            {
                s_transportExceptionHandler = value;
            }
        }

        // Returns true if the exception has been handled.  If it returns false or
        // throws a different exception, the original exception will be rethrown.
        public abstract bool HandleException(Exception exception);

        private class AlwaysHandleExceptionHandler : ExceptionHandler
        {
            public override bool HandleException(Exception exception)
            {
                return true;
            }
        }

        private class HandlerWrapper : Fx.ExceptionHandler
        {
            public HandlerWrapper(ExceptionHandler handler)
            {
                Fx.Assert(handler != null, "Cannot wrap a null handler.");
                Handler = handler;
            }

            public ExceptionHandler Handler { get; }

            public override bool HandleException(Exception exception)
            {
                return Handler.HandleException(exception);
            }
        }
    }
}
