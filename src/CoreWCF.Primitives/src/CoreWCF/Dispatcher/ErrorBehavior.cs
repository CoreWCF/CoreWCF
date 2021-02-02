﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class ErrorBehavior
    {
        private readonly bool _debug;
        private readonly bool _isOnServer;
        private readonly MessageVersion _messageVersion;

        internal ErrorBehavior(ChannelDispatcher channelDispatcher)
        {
            if (channelDispatcher?.ErrorHandlers == null)
            {
                Handlers = EmptyArray<IErrorHandler>.Allocate(0);
            }
            else
            {
                Handlers = EmptyArray<IErrorHandler>.ToArray(channelDispatcher.ErrorHandlers);
            }
            _debug = channelDispatcher.IncludeExceptionDetailInFaults;
            //isOnServer = channelDispatcher.IsOnServer;
            _isOnServer = true;
            _messageVersion = channelDispatcher.MessageVersion;
        }

        private void InitializeFault(MessageRpc rpc)
        {
            Exception error = rpc.Error;
            if (error is FaultException fault)
            {
                MessageFault messageFault = rpc.Operation.FaultFormatter.Serialize(fault, out string action);
                if (action == null)
                {
                    action = rpc.RequestVersion.Addressing.DefaultFaultAction;
                }

                if (messageFault != null)
                {
                    rpc.FaultInfo.Fault = Message.CreateMessage(rpc.RequestVersion, messageFault, action);
                }
            }
        }

        internal IErrorHandler[] Handlers { get; }

        internal void ProvideMessageFault(MessageRpc rpc)
        {
            if (rpc.Error != null)
            {
                ProvideMessageFaultCore(rpc);
            }
        }

        private void ProvideMessageFaultCore(MessageRpc rpc)
        {
            if (_messageVersion != rpc.RequestVersion)
            {
                Fx.Assert("CoreWCF.Dispatcher.ErrorBehavior.ProvideMessageFaultCore(): (this.messageVersion != rpc.RequestVersion)");
            }

            InitializeFault(rpc);

            ProvideFault(rpc.Error, rpc.Channel.GetProperty<FaultConverter>(), ref rpc.FaultInfo);

            ProvideMessageFaultCoreCoda(rpc);
        }

        private void ProvideFaultOfLastResort(Exception error, ref ErrorHandlerFaultInfo faultInfo)
        {
            if (faultInfo.Fault == null)
            {
                FaultCode code = new FaultCode(FaultCodeConstants.Codes.InternalServiceFault, FaultCodeConstants.Namespaces.NetDispatch);
                code = FaultCode.CreateReceiverFaultCode(code);
                string action = FaultCodeConstants.Actions.NetDispatcher;
                MessageFault fault;
                if (_debug)
                {
                    faultInfo.DefaultFaultAction = action;
                    fault = MessageFault.CreateFault(code, new FaultReason(error.Message), new ExceptionDetail(error));
                }
                else
                {
                    string reason = _isOnServer ? SR.SFxInternalServerError : SR.SFxInternalCallbackError;
                    fault = MessageFault.CreateFault(code, new FaultReason(reason));
                }
                faultInfo.IsConsideredUnhandled = true;
                faultInfo.Fault = Message.CreateMessage(_messageVersion, fault, action);
            }
            //if this is an InternalServiceFault coming from another service dispatcher we should treat it as unhandled so that the channels are cleaned up
            else if (error != null)
            {
                if (error is FaultException e && e.Fault != null && e.Fault.Code != null && e.Fault.Code.SubCode != null &&
                    string.Compare(e.Fault.Code.SubCode.Namespace, FaultCodeConstants.Namespaces.NetDispatch, StringComparison.Ordinal) == 0 &&
                    string.Compare(e.Fault.Code.SubCode.Name, FaultCodeConstants.Codes.InternalServiceFault, StringComparison.Ordinal) == 0)
                {
                    faultInfo.IsConsideredUnhandled = true;
                }
            }
        }

        private void ProvideMessageFaultCoreCoda(MessageRpc rpc)
        {
            if (rpc.FaultInfo.Fault.Headers.Action == null)
            {
                rpc.FaultInfo.Fault.Headers.Action = rpc.RequestVersion.Addressing.DefaultFaultAction;
            }

            rpc.Reply = rpc.FaultInfo.Fault;
        }

        internal void ProvideOnlyFaultOfLastResort(ref MessageRpc rpc)
        {
            ProvideFaultOfLastResort(rpc.Error, ref rpc.FaultInfo);
            ProvideMessageFaultCoreCoda(rpc);
        }

        internal void ProvideFault(Exception e, FaultConverter faultConverter, ref ErrorHandlerFaultInfo faultInfo)
        {
            ProvideWellKnownFault(e, faultConverter, ref faultInfo);
            for (int i = 0; i < Handlers.Length; i++)
            {
                Message m = faultInfo.Fault;
                Handlers[i].ProvideFault(e, _messageVersion, ref m);
                faultInfo.Fault = m;
                //if (TD.FaultProviderInvokedIsEnabled())
                //{
                //    TD.FaultProviderInvoked(handlers[i].GetType().FullName, e.Message);
                //}
            }
            ProvideFaultOfLastResort(e, ref faultInfo);
        }

        private void ProvideWellKnownFault(Exception e, FaultConverter faultConverter, ref ErrorHandlerFaultInfo faultInfo)
        {
            if (faultConverter != null && faultConverter.TryCreateFaultMessage(e, out Message faultMessage))
            {
                faultInfo.Fault = faultMessage;
                return;
            }
            else if (e is NetDispatcherFaultException)
            {
                NetDispatcherFaultException ndfe = e as NetDispatcherFaultException;
                if (_debug)
                {
                    ExceptionDetail detail = new ExceptionDetail(ndfe);
                    faultInfo.Fault = Message.CreateMessage(_messageVersion, MessageFault.CreateFault(ndfe.Code, ndfe.Reason, detail), ndfe.Action);
                }
                else
                {
                    faultInfo.Fault = Message.CreateMessage(_messageVersion, ndfe.CreateMessageFault(), ndfe.Action);
                }
            }
        }

        internal void HandleError(MessageRpc rpc)
        {
            if (rpc.Error != null)
            {
                HandleErrorCore(rpc);
            }
        }

        private void HandleErrorCore(MessageRpc rpc)
        {
            bool handled = HandleErrorCommon(rpc.Error, ref rpc.FaultInfo);
            if (handled)
            {
                rpc.Error = null;
            }
        }

        private bool HandleErrorCommon(Exception error, ref ErrorHandlerFaultInfo faultInfo)
        {
            bool handled;
            if (faultInfo.Fault != null   // there is a message
                && !faultInfo.IsConsideredUnhandled) // and it's not the internal-server-error one
            {
                handled = true;
            }
            else
            {
                handled = false;
            }

            try
            {
                //if (TD.ServiceExceptionIsEnabled())
                //{
                //    TD.ServiceException(null, error.ToString(), error.GetType().FullName);
                //}
                for (int i = 0; i < Handlers.Length; i++)
                {
                    bool handledByThis = Handlers[i].HandleError(error);
                    handled = handledByThis || handled;
                    //if (TD.ErrorHandlerInvokedIsEnabled())
                    //{
                    //    TD.ErrorHandlerInvoked(handlers[i].GetType().FullName, handledByThis, error.GetType().FullName);
                    //}
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }
            return handled;
        }

        internal bool HandleError(Exception error)
        {
            ErrorHandlerFaultInfo faultInfo = new ErrorHandlerFaultInfo(_messageVersion.Addressing.DefaultFaultAction);
            return HandleError(error, ref faultInfo);
        }

        internal bool HandleError(Exception error, ref ErrorHandlerFaultInfo faultInfo)
        {
            return HandleErrorCommon(error, ref faultInfo);
        }

        internal static bool ShouldRethrowExceptionAsIs(Exception e)
        {
            return true;
        }

        internal static bool ShouldRethrowClientSideExceptionAsIs(Exception e)
        {
            return true;
        }

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
                if (!ReferenceEquals(e, e2))
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