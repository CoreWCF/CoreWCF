// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading;

namespace CoreWCF.Channels
{
    internal class UriGenerator
    {
        private long _id;
        private readonly string _prefix;

        public UriGenerator()
            : this("uuid")
        {
        }

        public UriGenerator(string scheme)
            : this(scheme, ";")
        {
        }

        public UriGenerator(string scheme, string delimiter)
        {
            if (scheme == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("scheme"));
            }

            if (scheme.Length == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.UriGeneratorSchemeMustNotBeEmpty, "scheme"));
            }

            _prefix = string.Concat(scheme, ":", Guid.NewGuid().ToString(), delimiter, "id=");
        }

        public string Next()
        {
            long nextId = Interlocked.Increment(ref _id);
            return _prefix + nextId.ToString(CultureInfo.InvariantCulture);
        }
    }
}
