using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.ServiceModel
{
    [DataContract]
    public class ExceptionDetail
    {
        public ExceptionDetail(Exception exception)
        {
            if (exception == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("exception");
            }

            HelpLink = exception.HelpLink;
            Message = exception.Message;
            StackTrace = exception.StackTrace;
            Type = exception.GetType().ToString();

            if (exception.InnerException != null)
            {
                InnerException = new ExceptionDetail(exception.InnerException);
            }
        }

        [DataMember]
        public string HelpLink { get; set; }

        [DataMember]
        public ExceptionDetail InnerException { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string StackTrace { get; set; }

        [DataMember]
        public string Type { get; set; }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}\n{1}", SR.SFxExceptionDetailFormat, ToStringHelper(false));
        }

        internal string ToStringHelper(bool isInner)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}: {1}", Type, Message);
            if (InnerException != null)
            {
                sb.AppendFormat(" ----> {0}", InnerException.ToStringHelper(true));
            }
            else
            {
                sb.Append("\n");
            }
            sb.Append(StackTrace);
            if (isInner)
            {
                sb.AppendFormat("\n   {0}\n", SR.SFxExceptionDetailEndOfInner);
            }
            return sb.ToString();
        }
    }
}