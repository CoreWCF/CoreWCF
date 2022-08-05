// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace CoreWCF.Channels
{
    internal class ViaStringDecoder : StringDecoder
    {
        private Uri _via;

        public ViaStringDecoder(int sizeQuota)
            : base(sizeQuota)
        {
        }

        protected override Exception OnSizeQuotaExceeded(int size)
        {
            Exception result = new InvalidDataException(SR.Format(SR.FramingViaTooLong, size));
            return result;
        }

        protected override void OnComplete(string value)
        {
            try
            {
                _via = new Uri(value);
                base.OnComplete(value);
            }
            catch (UriFormatException exception)
            {
                throw new InvalidDataException(SR.Format(SR.FramingViaNotUri, value), exception);
            }
        }

        public Uri ValueAsUri
        {
            get
            {
                if (!IsValueDecoded)
                    throw new InvalidOperationException(SR.FramingValueNotAvailable);
                return _via;
            }
        }
    }
}
