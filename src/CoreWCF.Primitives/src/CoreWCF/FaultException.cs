using System;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;

namespace CoreWCF
{
    [KnownType(typeof(FaultException.FaultCodeData))]
    [KnownType(typeof(FaultException.FaultCodeData[]))]
    [KnownType(typeof(FaultException.FaultReasonData))]
    [KnownType(typeof(FaultException.FaultReasonData[]))]
    public class FaultException : CommunicationException
    {
        internal const string Namespace = "http://schemas.xmlsoap.org/Microsoft/WindowsCommunicationFoundation/2005/08/Faults/";

        string action;
        FaultCode code;
        FaultReason reason;
        MessageFault fault;
        public FaultException()
            : base(SR.SFxFaultReason)
        {
            code = FaultException.DefaultCode;
            reason = FaultException.DefaultReason;
        }

        internal FaultException(string reason, FaultCode code)
            : base(reason)
        {
            this.code = FaultException.EnsureCode(code);
            this.reason = FaultException.CreateReason(reason);
        }

        public FaultException(FaultReason reason, FaultCode code, string action)
            : base(FaultException.GetSafeReasonText(reason))
        {
            this.code = FaultException.EnsureCode(code);
            this.reason = FaultException.EnsureReason(reason);
            this.action = action;
        }

        internal FaultException(string reason, FaultCode code, string action, Exception innerException)
    : base(reason, innerException)
        {
            this.code = FaultException.EnsureCode(code);
            this.reason = FaultException.CreateReason(reason);
            this.action = action;
        }

        internal FaultException(FaultReason reason, FaultCode code, string action, Exception innerException)
    : base(FaultException.GetSafeReasonText(reason), innerException)
        {
            this.code = FaultException.EnsureCode(code);
            this.reason = FaultException.EnsureReason(reason);
            this.action = action;
        }

        public FaultException(MessageFault fault, string action)
            : base(FaultException.GetSafeReasonText(GetReason(fault)))
        {
            if (fault == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(fault));

            code = fault.Code;
            reason = fault.Reason;
            this.fault = fault;
            this.action = action;
        }

        // public on full framework
        internal FaultException(FaultReason reason, FaultCode code)
            : base(FaultException.GetSafeReasonText(reason))
        {
            this.code = FaultException.EnsureCode(code);
            this.reason = FaultException.EnsureReason(reason);
        }

        public string Action
        {
            get { return action; }
        }

        public FaultCode Code
        {
            get { return code; }
        }

        internal static FaultReason DefaultReason
        {
            get { return new FaultReason(SR.SFxFaultReason); }
        }

        internal static FaultCode DefaultCode
        {
            get { return new FaultCode("Sender"); }
        }

        public override string Message
        {
            get { return FaultException.GetSafeReasonText(Reason); }
        }

        public FaultReason Reason
        {
            get { return reason; }
        }

        internal MessageFault Fault
        {
            get { return fault; }
        }

        public static FaultException CreateFault(MessageFault messageFault, params Type[] faultDetailTypes)
        {
            return CreateFault(messageFault, null, faultDetailTypes);
        }

        public static FaultException CreateFault(MessageFault messageFault, string action, params Type[] faultDetailTypes)
        {
            if (messageFault == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageFault));
            }

            if (faultDetailTypes == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(faultDetailTypes));
            }
            DataContractSerializerFaultFormatter faultFormatter = new DataContractSerializerFaultFormatter(faultDetailTypes);
            return faultFormatter.Deserialize(messageFault, action);
        }

        public virtual Channels.MessageFault CreateMessageFault() { return default(Channels.MessageFault); }

        internal static string GetSafeReasonText(FaultReason reason)
        {
            if (reason == null)
                return SR.SFxUnknownFaultNullReason0;

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

        static FaultReason CreateReason(string reason)
        {
            return (reason != null) ? new FaultReason(reason) : DefaultReason;
        }

        static FaultReason GetReason(MessageFault fault)
        {
            if (fault == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(fault));
            }
            return fault.Reason;
        }

        static FaultCode EnsureCode(FaultCode code)
        {
            return code ?? DefaultCode;
        }

        static FaultReason EnsureReason(FaultReason reason)
        {
            return reason ?? DefaultReason;
        }

        internal class FaultCodeData
        {
            string name;
            string ns;

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
                FaultCodeData[] array = new FaultCodeData[FaultCodeData.GetDepth(code)];

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

            static int GetDepth(FaultCode code)
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

        //[Serializable]
        internal class FaultReasonData
        {
            string xmlLang;
            string text;

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

    public class FaultException<TDetail> : FaultException
    {
        public FaultException(TDetail detail, FaultReason reason, FaultCode code, string action) { }
        public TDetail Detail { get { return default(TDetail); } }
        public override Channels.MessageFault CreateMessageFault() { return default(Channels.MessageFault); }
        public override string ToString() { return default(string); }
    }
}