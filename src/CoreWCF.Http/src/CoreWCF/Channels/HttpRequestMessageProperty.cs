using System.Net.Http.Headers;

namespace CoreWCF.Channels
{
    public sealed class HttpRequestMessageProperty : IMessageProperty
    {
        private string _method;

        private string _queryString;

        public HttpHeaders Headers { get; } = new ServiceModelHttpHeaders();

        public string Method
        {
            get { return _method; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _method = value;
            }
        }

        public static string Name => "httpRequest";

        public string QueryString
        {
            get { return _queryString; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _queryString = value;
            }
        }

        IMessageProperty IMessageProperty.CreateCopy()
        {
            return this;
        }
    }
}