// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF
{
    public enum WSMessageEncoding
    {
        Text = 0,
        Mtom,
    }

    internal static class WSMessageEncodingHelper
    {
        internal static bool IsDefined(WSMessageEncoding value)
        {
            return
                value == WSMessageEncoding.Text
                || value == WSMessageEncoding.Mtom;
        }

        internal static void SyncUpEncodingBindingElementProperties(TextMessageEncodingBindingElement textEncoding, MtomMessageEncodingBindingElement mtomEncoding)
        {
            // textEncoding provides the backing store for ReaderQuotas and WriteEncoding,
            // we must ensure same values propogate to mtomEncoding
            textEncoding.ReaderQuotas.CopyTo(mtomEncoding.ReaderQuotas);
            mtomEncoding.WriteEncoding = textEncoding.WriteEncoding;
        }
    }
}
