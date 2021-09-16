// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel.Protocols.WSTrust
{
    /// <summary>
    /// A class encapsulating the result of a WS-Trust request.
    /// </summary>
    public class Status
    {
        private string _code;
        private string _reason;

        /// <summary>
        /// Creates an instance of Status
        /// </summary>
        /// <param name="code">Status code.</param>
        /// <param name="reason">Optional status reason.</param>
        public Status(string code, string reason)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(code));
            }

            _code = code;
            _reason = reason;
        }

        /// <summary>
        /// Gets or sets the status code for the validation binding in the RSTR.
        /// </summary>
        public string Code
        {
            get
            {
                return _code;
            }

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("code");
                }

                _code = value;
            }
        }

        /// <summary>
        /// Gets or sets the optional status reason for the validation binding in the RSTR.
        /// </summary>
        public string Reason
        {
            get
            {
                return _reason;
            }

            set
            {
                _reason = value;
            }
        }
    }
}
