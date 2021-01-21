// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF
{
    [Serializable]
    [KnownType(typeof(FaultCodeData))]
    [KnownType(typeof(FaultCodeData[]))]
    [KnownType(typeof(FaultReasonData))]
    [KnownType(typeof(FaultReasonData[]))]
    public class FaultException : CommunicationException
    {
        internal const string Namespace = "http://schemas.xmlsoap.org/Microsoft/WindowsCommunicationFoundation/2005/08/Faults/";
        private readonly string _action;
        private readonly FaultCode _code;
        private readonly FaultReason _reason;
        private readonly MessageFault _fault;

        public FaultException()
            : base(SR.SFxFaultReason)
        {
            _code = DefaultCode;
            _reason = DefaultReason;
        }

        public FaultException(string reason)
            : base(reason)
        {
            _code = DefaultCode;
            _reason = CreateReason(reason);
        }

        public FaultException(FaultReason reason)
            : base(GetSafeReasonText(reason))
        {
            _code = DefaultCode;
            _reason = EnsureReason(reason);
        }

        public FaultException(string reason, FaultCode code)
            : base(reason)
        {
            _code = EnsureCode(code);
            _reason = CreateReason(reason);
        }

        public FaultException(FaultReason reason, FaultCode code)
            : base(GetSafeReasonText(reason))
        {
            _code = EnsureCode(code);
            _reason = EnsureReason(reason);
        }

        public FaultException(string reason, FaultCode code, string action)
            : base(reason)
        {
            _code = EnsureCode(code);
            _reason = CreateReason(reason);
            _action = action;
        }

        internal FaultException(string reason, FaultCode code, string action, Exception innerException)
            : base(reason, innerException)
        {
            _code = EnsureCode(code);
            _reason = CreateReason(reason);
            _action = action;
        }

        public FaultException(FaultReason reason, FaultCode code, string action)
            : base(GetSafeReasonText(reason))
        {
            _code = EnsureCode(code);
            _reason = EnsureReason(reason);
            _action = action;
        }

        internal FaultException(FaultReason reason, FaultCode code, string action, Exception innerException)
            : base(GetSafeReasonText(reason), innerException)
        {
            _code = EnsureCode(code);
            _reason = EnsureReason(reason);
            _action = action;
        }

        public FaultException(MessageFault fault)
            : base(GetSafeReasonText(GetReason(fault)))
        {
            if (fault == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("fault");
            }

            _code = EnsureCode(fault.Code);
            _reason = EnsureReason(fault.Reason);
            _fault = fault;
        }

        public FaultException(MessageFault fault, string action)
            : base(GetSafeReasonText(GetReason(fault)))
        {
            if (fault == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("fault");
            }

            _code = fault.Code;
            _reason = fault.Reason;
            _fault = fault;
            _action = action;
        }

        protected FaultException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _code = ReconstructFaultCode(info, "code");
            _reason = ReconstructFaultReason(info, "reason");
            _fault = (MessageFault)info.GetValue("messageFault", typeof(MessageFault));
            _action = (string)info.GetString("action");
        }

        public string Action
        {
            get { return _action; }
        }

        public FaultCode Code
        {
            get { return _code; }
        }

        private static FaultReason DefaultReason
        {
            get { return new FaultReason(SR.SFxFaultReason); }
        }

        private static FaultCode DefaultCode
        {
            get { return new FaultCode("Sender"); }
        }

        public override string Message
        {
            get { return GetSafeReasonText(Reason); }
        }

        public FaultReason Reason
        {
            get { return _reason; }
        }

        internal MessageFault Fault
        {
            get { return _fault; }
        }

        internal void AddFaultCodeObjectData(SerializationInfo info, string key, FaultCode code)
        {
            info.AddValue(key, FaultCodeData.GetObjectData(code));
        }

        internal void AddFaultReasonObjectData(SerializationInfo info, string key, FaultReason reason)
        {
            info.AddValue(key, FaultReasonData.GetObjectData(reason));
        }

        private static FaultCode CreateCode(string code)
        {
            return (code != null) ? new FaultCode(code) : DefaultCode;
        }

        public static FaultException CreateFault(MessageFault messageFault, params Type[] faultDetailTypes)
        {
            return CreateFault(messageFault, null, faultDetailTypes);
        }

        public static FaultException CreateFault(MessageFault messageFault, string action, params Type[] faultDetailTypes)
        {
            if (messageFault == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("messageFault");
            }

            if (faultDetailTypes == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("faultDetailTypes");
            }
            var faultFormatter = new DataContractSerializerFaultFormatter(faultDetailTypes);
            return faultFormatter.Deserialize(messageFault, action);
        }

        public virtual MessageFault CreateMessageFault()
        {
            if (_fault != null)
            {
                return _fault;
            }
            else
            {
                return MessageFault.CreateFault(_code, _reason);
            }
        }

        private static FaultReason CreateReason(string reason)
        {
            return (reason != null) ? new FaultReason(reason) : DefaultReason;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            AddFaultCodeObjectData(info, "code", _code);
            AddFaultReasonObjectData(info, "reason", _reason);
            info.AddValue("messageFault", _fault);
            info.AddValue("action", _action);
        }

        private static FaultReason GetReason(MessageFault fault)
        {
            if (fault == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(fault));
            }
            return fault.Reason;
        }

        internal static string GetSafeReasonText(MessageFault messageFault)
        {
            if (messageFault == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageFault));
            }

            return GetSafeReasonText(messageFault.Reason);
        }

        internal static string GetSafeReasonText(FaultReason reason)
        {
            if (reason == null)
            {
                return SR.SFxUnknownFaultNullReason0;
            }

            try
            {
                return reason.GetMatchingTranslation(System.Globalization.CultureInfo.CurrentCulture).Text;
            }
            catch (ArgumentException)
            {
                if (reason.Translations.Count == 0)
                {
                    return SR.SFxUnknownFaultZeroReasons0;
                }
                else
                {
                    return SR.Format(SR.SFxUnknownFaultNoMatchingTranslation1, reason.Translations[0].Text);
                }
            }
        }

        private static FaultCode EnsureCode(FaultCode code)
        {
            return (code != null) ? code : DefaultCode;
        }

        private static FaultReason EnsureReason(FaultReason reason)
        {
            return (reason != null) ? reason : DefaultReason;
        }

        internal FaultCode ReconstructFaultCode(SerializationInfo info, string key)
        {
            FaultCodeData[] data = (FaultCodeData[])info.GetValue(key, typeof(FaultCodeData[]));
            return FaultCodeData.Construct(data);
        }

        internal FaultReason ReconstructFaultReason(SerializationInfo info, string key)
        {
            FaultReasonData[] data = (FaultReasonData[])info.GetValue(key, typeof(FaultReasonData[]));
            return FaultReasonData.Construct(data);
        }

        [Serializable]
        internal class FaultCodeData
        {
#pragma warning disable IDE1006 // Naming Styles - class is Serializable so can't change field names
            private string name;
            private string ns;
#pragma warning restore IDE1006 // Naming Styles

            internal static FaultCode Construct(FaultCodeData[] nodes)
            {
                FaultCode code = null;

                for (int i = nodes.Length - 1; i >= 0; i--)
                {
                    code = new FaultCode(nodes[i].name, nodes[i].ns, code);
                }

                return code;
            }

            internal static FaultCodeData[] GetObjectData(FaultCode code)
            {
                FaultCodeData[] array = new FaultCodeData[GetDepth(code)];

                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = new FaultCodeData();
                    array[i].name = code.Name;
                    array[i].ns = code.Namespace;
                    code = code.SubCode;
                }

                if (code != null)
                {
                    Fx.Assert("FaultException.FaultCodeData.GetObjectData: (code != null)");
                }
                return array;
            }

            private static int GetDepth(FaultCode code)
            {
                int depth = 0;

                while (code != null)
                {
                    depth++;
                    code = code.SubCode;
                }

                return depth;
            }
        }

        [Serializable]
        internal class FaultReasonData
        {
#pragma warning disable IDE1006 // Naming Styles - class is Serializable so can't change field names
            private string xmlLang;
            private string text;
#pragma warning restore IDE1006 // Naming Styles

            internal static FaultReason Construct(FaultReasonData[] nodes)
            {
                FaultReasonText[] reasons = new FaultReasonText[nodes.Length];

                for (int i = 0; i < nodes.Length; i++)
                {
                    reasons[i] = new FaultReasonText(nodes[i].text, nodes[i].xmlLang);
                }

                return new FaultReason(reasons);
            }

            internal static FaultReasonData[] GetObjectData(FaultReason reason)
            {
                ReadOnlyCollection<FaultReasonText> translations = reason.Translations;
                FaultReasonData[] array = new FaultReasonData[translations.Count];

                for (int i = 0; i < translations.Count; i++)
                {
                    array[i] = new FaultReasonData();
                    array[i].xmlLang = translations[i].XmlLang;
                    array[i].text = translations[i].Text;
                }

                return array;
            }
        }
    }

    [Serializable]
    public class FaultException<TDetail> : FaultException
    {
        public FaultException(TDetail detail)
            : base()
        {
            Detail = detail;
        }

        public FaultException(TDetail detail, string reason)
            : base(reason)
        {
            Detail = detail;
        }

        public FaultException(TDetail detail, FaultReason reason)
            : base(reason)
        {
            Detail = detail;
        }

        public FaultException(TDetail detail, string reason, FaultCode code)
            : base(reason, code)
        {
            Detail = detail;
        }

        public FaultException(TDetail detail, FaultReason reason, FaultCode code)
            : base(reason, code)
        {
            Detail = detail;
        }

        public FaultException(TDetail detail, string reason, FaultCode code, string action)
            : base(reason, code, action)
        {
            Detail = detail;
        }

        public FaultException(TDetail detail, FaultReason reason, FaultCode code, string action)
            : base(reason, code, action)
        {
            Detail = detail;
        }

        protected FaultException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Detail = (TDetail)info.GetValue("detail", typeof(TDetail));
        }

        public TDetail Detail { get; }

        public override MessageFault CreateMessageFault()
        {
            return MessageFault.CreateFault(Code, Reason, Detail);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("detail", Detail);
        }

        public override string ToString()
        {
            return SR.Format(SR.SFxFaultExceptionToString3, GetType(), Message, Detail != null ? Detail.ToString() : string.Empty);
        }
    }
}