using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Security;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class ProxyOperationRuntime
    {
        static internal readonly ParameterInfo[] NoParams = new ParameterInfo[0];
        static internal readonly object[] EmptyArray = new object[0];

        readonly IClientMessageFormatter _formatter;
        readonly bool _isInitiating;
        readonly bool _isOneWay;
        readonly bool _isTerminating;
        readonly bool _isSessionOpenNotificationEnabled;
        readonly string _name;
        readonly IParameterInspector[] _parameterInspectors;
        readonly IClientFaultFormatter _faultFormatter;
        readonly ImmutableClientRuntime _parent;
        bool _serializeRequest;
        bool _deserializeReply;
        string _action;
        string _replyAction;

        MethodInfo _beginMethod;
        MethodInfo _syncMethod;
        MethodInfo _taskMethod;
        ParameterInfo[] _inParams;
        ParameterInfo[] _outParams;
        ParameterInfo[] _endOutParams;
        ParameterInfo _returnParam;

        internal ProxyOperationRuntime(ClientOperation operation, ImmutableClientRuntime parent)
        {
            if (operation == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("operation");
            if (parent == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("parent");

            _parent = parent;
            _formatter = operation.Formatter;
            _isInitiating = operation.IsInitiating;
            _isOneWay = operation.IsOneWay;
            _isTerminating = operation.IsTerminating;
            _isSessionOpenNotificationEnabled = operation.IsSessionOpenNotificationEnabled;
            _name = operation.Name;
            _parameterInspectors = EmptyArray<IParameterInspector>.ToArray(operation.ParameterInspectors);
            _faultFormatter = operation.FaultFormatter;
            _serializeRequest = operation.SerializeRequest;
            _deserializeReply = operation.DeserializeReply;
            _action = operation.Action;
            _replyAction = operation.ReplyAction;
            _beginMethod = operation.BeginMethod;
            _syncMethod = operation.SyncMethod;
            _taskMethod = operation.TaskMethod;
            TaskTResult = operation.TaskTResult;

            if (_beginMethod != null)
            {
                _inParams = ServiceReflector.GetInputParameters(_beginMethod, true);
                if (_syncMethod != null)
                {
                    _outParams = ServiceReflector.GetOutputParameters(_syncMethod, false);
                }
                else
                {
                    _outParams = NoParams;
                }
                _endOutParams = ServiceReflector.GetOutputParameters(operation.EndMethod, true);
                _returnParam = operation.EndMethod.ReturnParameter;
            }
            else if (_syncMethod != null)
            {
                _inParams = ServiceReflector.GetInputParameters(_syncMethod, false);
                _outParams = ServiceReflector.GetOutputParameters(_syncMethod, false);
                _returnParam = _syncMethod.ReturnParameter;
            }

            if (_formatter == null && (_serializeRequest || _deserializeReply))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ClientRuntimeRequiresFormatter0, _name)));
            }
        }

        internal string Action
        {
            get { return _action; }
        }

        internal IClientFaultFormatter FaultFormatter
        {
            get { return _faultFormatter; }
        }

        internal bool IsInitiating
        {
            get { return _isInitiating; }
        }

        internal bool IsOneWay
        {
            get { return _isOneWay; }
        }

        internal bool IsTerminating
        {
            get { return _isTerminating; }
        }

        internal bool IsSessionOpenNotificationEnabled
        {
            get { return _isSessionOpenNotificationEnabled; }
        }

        internal string Name
        {
            get { return _name; }
        }

        internal ImmutableClientRuntime Parent
        {
            get { return _parent; }
        }

        internal string ReplyAction
        {
            get { return _replyAction; }
        }

        internal bool DeserializeReply
        {
            get { return _deserializeReply; }
        }

        internal bool SerializeRequest
        {
            get { return _serializeRequest; }
        }

        internal Type TaskTResult
        {
            get;
            set;
        }

        internal void AfterReply(ref ProxyRpc rpc)
        {
            if (!_isOneWay)
            {
                Message reply = rpc.Reply;

                if (_deserializeReply)
                {
                    //if (TD.ClientFormatterDeserializeReplyStartIsEnabled())
                    //{
                    //    TD.ClientFormatterDeserializeReplyStart(rpc.EventTraceActivity);
                    //}

                    rpc.ReturnValue = _formatter.DeserializeReply(reply, rpc.OutputParameters);

                    //if (TD.ClientFormatterDeserializeReplyStopIsEnabled())
                    //{
                    //    TD.ClientFormatterDeserializeReplyStop(rpc.EventTraceActivity);
                    //}

                }
                else
                {
                    rpc.ReturnValue = reply;
                }

                int offset = _parent.ParameterInspectorCorrelationOffset;
                try
                {
                    for (int i = _parameterInspectors.Length - 1; i >= 0; i--)
                    {
                        _parameterInspectors[i].AfterCall(_name,
                                                              rpc.OutputParameters,
                                                              rpc.ReturnValue,
                                                              rpc.Correlation[offset + i]);
                        //if (TD.ClientParameterInspectorAfterCallInvokedIsEnabled())
                        //{
                        //    TD.ClientParameterInspectorAfterCallInvoked(rpc.EventTraceActivity, this._parameterInspectors[i].GetType().FullName);
                        //}
                    }
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    if (ErrorBehavior.ShouldRethrowClientSideExceptionAsIs(e))
                    {
                        throw;
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
                }

                if (_parent.ValidateMustUnderstand)
                {
                    Collection<MessageHeaderInfo> headersNotUnderstood = reply.Headers.GetHeadersNotUnderstood();
                    if (headersNotUnderstood != null && headersNotUnderstood.Count > 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.Format(SR.SFxHeaderNotUnderstood, headersNotUnderstood[0].Name, headersNotUnderstood[0].Namespace)));
                    }
                }
            }
        }

        internal void BeforeRequest(ref ProxyRpc rpc)
        {
            int offset = _parent.ParameterInspectorCorrelationOffset;
            try
            {
                for (int i = 0; i < _parameterInspectors.Length; i++)
                {
                    rpc.Correlation[offset + i] = _parameterInspectors[i].BeforeCall(_name, rpc.InputParameters);
                    //if (TD.ClientParameterInspectorBeforeCallInvokedIsEnabled())
                    //{
                    //    TD.ClientParameterInspectorBeforeCallInvoked(rpc.EventTraceActivity, this._parameterInspectors[i].GetType().FullName);
                    //}
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (ErrorBehavior.ShouldRethrowClientSideExceptionAsIs(e))
                {
                    throw;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }

            if (_serializeRequest)
            {
                //if (TD.ClientFormatterSerializeRequestStartIsEnabled())
                //{
                //    TD.ClientFormatterSerializeRequestStart(rpc.EventTraceActivity);
                //}

                rpc.Request = _formatter.SerializeRequest(rpc.MessageVersion, rpc.InputParameters);



                //if (TD.ClientFormatterSerializeRequestStopIsEnabled())
                //{
                //    TD.ClientFormatterSerializeRequestStop(rpc.EventTraceActivity);
                //}
            }
            else
            {
                if (rpc.InputParameters[0] == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxProxyRuntimeMessageCannotBeNull, _name)));
                }

                rpc.Request = (Message)rpc.InputParameters[0];
                if (!IsValidAction(rpc.Request, Action))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInvalidRequestAction, Name, rpc.Request.Headers.Action ?? "{NULL}", Action)));
            }
        }

        internal static object GetDefaultParameterValue(Type parameterType)
        {
            return (parameterType.GetTypeInfo().IsValueType && parameterType != typeof(void)) ? Activator.CreateInstance(parameterType) : null;
        }

        internal bool IsSyncCall(MethodCall methodCall)
        {
            if (_syncMethod == null)
            {
                return false;
            }

            Contract.Assert(methodCall != null);
            Contract.Assert(methodCall.MethodBase != null);
            return methodCall.MethodBase.Equals(_syncMethod);
        }

        internal bool IsBeginCall(MethodCall methodCall)
        {
            if (_beginMethod == null)
            {
                return false;
            }

            Contract.Assert(methodCall != null);
            Contract.Assert(methodCall.MethodBase != null);
            return methodCall.MethodBase.Equals(_beginMethod);
        }

        internal bool IsTaskCall(MethodCall methodCall)
        {
            if (_taskMethod == null)
            {
                return false;
            }

            Contract.Assert(methodCall != null);
            Contract.Assert(methodCall.MethodBase != null);
            return methodCall.MethodBase.Equals(_taskMethod);
        }

        internal object[] MapSyncInputs(MethodCall methodCall, out object[] outs)
        {
            if (_outParams.Length == 0)
            {
                outs = Array.Empty<object>();
            }
            else
            {
                outs = new object[_outParams.Length];
            }
            if (_inParams.Length == 0)
                return Array.Empty<object>();
            return methodCall.Args;
        }

        internal object[] MapAsyncBeginInputs(MethodCall methodCall, out AsyncCallback callback, out object asyncState)
        {
            object[] ins;
            if (_inParams.Length == 0)
            {
                ins = Array.Empty<object>();
            }
            else
            {
                ins = new object[_inParams.Length];
            }

            object[] args = methodCall.Args;
            for (int i = 0; i < ins.Length; i++)
            {
                ins[i] = args[_inParams[i].Position];
            }

            callback = args[methodCall.Args.Length - 2] as AsyncCallback;
            asyncState = args[methodCall.Args.Length - 1];
            return ins;
        }

        internal void MapAsyncEndInputs(MethodCall methodCall, out IAsyncResult result, out object[] outs)
        {
            outs = new object[_endOutParams.Length];
            result = methodCall.Args[methodCall.Args.Length - 1] as IAsyncResult;
        }

        internal object[] MapSyncOutputs(MethodCall methodCall, object[] outs, ref object ret)
        {
            return MapOutputs(_outParams, methodCall, outs, ref ret);
        }

        internal object[] MapAsyncOutputs(MethodCall methodCall, object[] outs, ref object ret)
        {
            return MapOutputs(_endOutParams, methodCall, outs, ref ret);
        }

        private object[] MapOutputs(ParameterInfo[] parameters, MethodCall methodCall, object[] outs, ref object ret)
        {
            if (ret == null && _returnParam != null)
            {
                ret = GetDefaultParameterValue(TypeLoader.GetParameterType(_returnParam));
            }

            if (parameters.Length == 0)
            {
                return null;
            }

            object[] args = methodCall.Args;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (outs[i] == null)
                {
                    // the RealProxy infrastructure requires a default value for value types
                    args[parameters[i].Position] = GetDefaultParameterValue(TypeLoader.GetParameterType(parameters[i]));
                }
                else
                {
                    args[parameters[i].Position] = outs[i];
                }
            }

            return args;
        }

        static internal bool IsValidAction(Message message, string action)
        {
            if (message == null)
            {
                return false;
            }

            if (message.IsFault)
            {
                return true;
            }

            if (action == MessageHeaders.WildcardAction)
            {
                return true;
            }

            return (string.CompareOrdinal(message.Headers.Action, action) == 0);
        }
    }

}