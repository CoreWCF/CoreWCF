// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Security
{
    internal class LaxModeSecurityHeaderElementInferenceEngine : SecurityHeaderElementInferenceEngine
    {
        protected LaxModeSecurityHeaderElementInferenceEngine() { }

        internal static LaxModeSecurityHeaderElementInferenceEngine Instance { get; } = new LaxModeSecurityHeaderElementInferenceEngine();

        public override async ValueTask ExecuteProcessingPassesAsync(ReceiveSecurityHeader securityHeader, XmlDictionaryReader reader)
        {
            // pass 1
            await securityHeader.ExecuteReadingPassAsync(reader);

            // pass 1.5
            securityHeader.ExecuteDerivedKeyTokenStubPass(false);

            // pass 2
            await securityHeader.ExecuteSubheaderDecryptionPassAsync();

            // pass 2.5
            securityHeader.ExecuteDerivedKeyTokenStubPass(true);

            // layout-specific inferences
            MarkElements(securityHeader.ElementManager, securityHeader.RequireMessageProtection);

            // pass 3
            securityHeader.ExecuteSignatureEncryptionProcessingPass();
        }

        public override void MarkElements(ReceiveSecurityHeaderElementManager elementManager, bool messageSecurityMode)
        {
            bool primarySignatureFound = false;
            for (int position = 0; position < elementManager.Count; position++)
            {
                elementManager.GetElementEntry(position, out ReceiveSecurityHeaderEntry entry);
                if (entry.elementCategory == ReceiveSecurityHeaderElementCategory.Signature)
                {
                    if (!messageSecurityMode)
                    {
                        elementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Endorsing);
                        continue;
                    }
                    SignedXml signedXml = (SignedXml)entry.element;

                    SignedInfo signedInfo = signedXml.Signature.SignedInfo;
                    bool targetsSignature = false;
                    if (signedInfo.References.Count == 1)
                    {
                        Reference signedXmlReference = (Reference)signedInfo.References[0];
                        string uri = signedXmlReference.Uri;
                        string id;
                        if (uri != null && uri.Length > 1 && uri[0] == '#')
                        {
                            id = uri.Substring(1);
                        }
                        else
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                new MessageSecurityException(SR.Format(SR.UnableToResolveReferenceUriForSignature, uri)));
                        }
                        for (int j = 0; j < elementManager.Count; j++)
                        {
                            elementManager.GetElementEntry(j, out ReceiveSecurityHeaderEntry inner);
                            if (j != position && inner.elementCategory == ReceiveSecurityHeaderElementCategory.Signature && inner.id == id)
                            {
                                targetsSignature = true;
                                break;
                            }
                        }
                    }
                    if (targetsSignature)
                    {
                        elementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Endorsing);
                        continue;
                    }
                    else
                    {
                        if (primarySignatureFound)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.AtMostOnePrimarySignatureInReceiveSecurityHeader)));
                        }
                        primarySignatureFound = true;
                        elementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Primary);
                        continue;
                    }
                }
            }
        }
    }
}
