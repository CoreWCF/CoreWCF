// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.Xml;
using System.Xml;
using CoreWCF.Diagnostics;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using ISignatureValueSecurityElement = CoreWCF.IdentityModel.ISignatureValueSecurityElement;

namespace CoreWCF.Security
{
    internal interface ISignatureReaderProvider
    {
        XmlDictionaryReader GetReader(object callbackContext);
    }

    internal sealed class ReceiveSecurityHeaderElementManager : ISignatureReaderProvider
    {
        private const int InitialCapacity = 8;
        private readonly ReceiveSecurityHeader securityHeader;
        private ReceiveSecurityHeaderEntry[] elements;
        private int count;
        private readonly string[] headerIds;
        private string[] predecryptionHeaderIds;
        private string bodyId;
        private string bodyContentId;
        private bool isPrimaryTokenSigned = false;

        public ReceiveSecurityHeaderElementManager(ReceiveSecurityHeader securityHeader)
        {
            this.securityHeader = securityHeader;
            elements = new ReceiveSecurityHeaderEntry[InitialCapacity];
            if (securityHeader.RequireMessageProtection)
            {
                headerIds = new string[securityHeader.ProcessedMessage.Headers.Count];
            }
        }

        public int Count => count;

        public bool IsPrimaryTokenSigned
        {
            get { return isPrimaryTokenSigned; }
            set { isPrimaryTokenSigned = value; }
        }

        public void AppendElement(
            ReceiveSecurityHeaderElementCategory elementCategory, object element,
            ReceiveSecurityHeaderBindingModes bindingMode, string id, TokenTracker supportingTokenTracker)
        {
            if (id != null)
            {
                VerifyIdUniquenessInSecurityHeader(id);
            }
            EnsureCapacityToAdd();
            elements[count++].SetElement(elementCategory, element, bindingMode, id, false, null, supportingTokenTracker);
        }

        public void AppendSignature(SignedXml signedXml)
        {
            AppendElement(ReceiveSecurityHeaderElementCategory.Signature, signedXml,
                ReceiveSecurityHeaderBindingModes.Unknown, signedXml.Signature.Id, null);
        }

        public void AppendReferenceList(ReferenceList referenceList)
        {
            AppendElement(ReceiveSecurityHeaderElementCategory.ReferenceList, referenceList,
                ReceiveSecurityHeaderBindingModes.Unknown, null, null);
        }

        public void AppendEncryptedData(EncryptedData encryptedData)
        {
            AppendElement(ReceiveSecurityHeaderElementCategory.EncryptedData, encryptedData,
                ReceiveSecurityHeaderBindingModes.Unknown, encryptedData.Id, null);
        }

        public void AppendSignatureConfirmation(ISignatureValueSecurityElement signatureConfirmationElement)
        {
            AppendElement(ReceiveSecurityHeaderElementCategory.SignatureConfirmation, signatureConfirmationElement,
                ReceiveSecurityHeaderBindingModes.Unknown, signatureConfirmationElement.Id, null);
        }

        public void AppendTimestamp(SecurityTimestamp timestamp)
        {
            AppendElement(ReceiveSecurityHeaderElementCategory.Timestamp, timestamp,
                ReceiveSecurityHeaderBindingModes.Unknown, timestamp.Id, null);
        }

        public void AppendSecurityTokenReference(SecurityKeyIdentifierClause strClause, string strId)
        {
            if (!String.IsNullOrEmpty(strId))
            {
                VerifyIdUniquenessInSecurityHeader(strId);
                AppendElement(ReceiveSecurityHeaderElementCategory.SecurityTokenReference, strClause, ReceiveSecurityHeaderBindingModes.Unknown, strId, null);
            }
        }

        public void AppendToken(SecurityToken token, ReceiveSecurityHeaderBindingModes mode, TokenTracker supportingTokenTracker)
        {
            AppendElement(ReceiveSecurityHeaderElementCategory.Token, token,
                mode, token.Id, supportingTokenTracker);
        }

        public void EnsureAllRequiredSecurityHeaderTargetsWereProtected()
        {
            Fx.Assert(securityHeader.RequireMessageProtection, "security header protection checks should only be done for message security");
            for (int i = 0; i < count; i++)
            {
                GetElementEntry(i, out ReceiveSecurityHeaderEntry entry);
                if (!entry.signed)
                {
                    switch (entry.elementCategory)
                    {
                        case ReceiveSecurityHeaderElementCategory.Timestamp:
                        case ReceiveSecurityHeaderElementCategory.SignatureConfirmation:
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                new MessageSecurityException(SR.Format(SR.RequiredSecurityHeaderElementNotSigned, entry.elementCategory, entry.id)));
                        case ReceiveSecurityHeaderElementCategory.Token:
                            switch (entry.bindingMode)
                            {
                                case ReceiveSecurityHeaderBindingModes.Signed:
                                case ReceiveSecurityHeaderBindingModes.SignedEndorsing:
                                case ReceiveSecurityHeaderBindingModes.Basic:
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                        new MessageSecurityException(SR.Format(SR.RequiredSecurityTokenNotSigned, entry.element, entry.bindingMode)));
                            }
                            break;
                    }
                }

                if (!entry.encrypted)
                {
                    if (entry.elementCategory == ReceiveSecurityHeaderElementCategory.Token &&
                        entry.bindingMode == ReceiveSecurityHeaderBindingModes.Basic)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            new MessageSecurityException(SR.Format(SR.RequiredSecurityTokenNotEncrypted, entry.element, entry.bindingMode)));
                    }
                }
            }
        }

        private void EnsureCapacityToAdd()
        {
            if (count == elements.Length)
            {
                ReceiveSecurityHeaderEntry[] newElements = new ReceiveSecurityHeaderEntry[elements.Length * 2];
                Array.Copy(elements, 0, newElements, 0, count);
                elements = newElements;
            }
        }

        public object GetElement(int index)
        {
            Fx.Assert(0 <= index && index < count, "");
            return elements[index].element;
        }

        public T GetElement<T>(int index) where T : class
        {
            Fx.Assert(0 <= index && index < count, "");
            return (T)elements[index].element;
        }

        public void GetElementEntry(int index, out ReceiveSecurityHeaderEntry element)
        {
            Fx.Assert(0 <= index && index < count, "index out of range");
            element = elements[index];
        }

        public ReceiveSecurityHeaderElementCategory GetElementCategory(int index)
        {
            Fx.Assert(0 <= index && index < count, "index out of range");
            return elements[index].elementCategory;
        }

        public void GetPrimarySignature(out XmlDictionaryReader reader, out string id)
        {
            for (int i = 0; i < count; i++)
            {
                GetElementEntry(i, out ReceiveSecurityHeaderEntry entry);
                if (entry.elementCategory == ReceiveSecurityHeaderElementCategory.Signature &&
                    entry.bindingMode == ReceiveSecurityHeaderBindingModes.Primary)
                {
                    reader = GetReader(i, false);
                    id = entry.id;
                    return;
                }
            }
            reader = null;
            id = null;
            return;
        }

        internal XmlDictionaryReader GetReader(int index, bool requiresEncryptedFormReader)
        {
            Fx.Assert(0 <= index && index < count, "index out of range");
            if (!requiresEncryptedFormReader)
            {
                byte[] decryptedBuffer = elements[index].decryptedBuffer;
                if (decryptedBuffer != null)
                {
                    return securityHeader.CreateDecryptedReader(decryptedBuffer);
                }
            }
            XmlDictionaryReader securityHeaderReader = securityHeader.CreateSecurityHeaderReader();
            securityHeaderReader.ReadStartElement();
            for (int i = 0; securityHeaderReader.IsStartElement() && i < index; i++)
            {
                securityHeaderReader.Skip();
            }
            return securityHeaderReader;
        }

        public XmlDictionaryReader GetSignatureVerificationReader(string id, bool requiresEncryptedFormReaderIfDecrypted)
        {
            for (int i = 0; i < count; i++)
            {
                GetElementEntry(i, out ReceiveSecurityHeaderEntry entry);
                bool encryptedForm = entry.encrypted && requiresEncryptedFormReaderIfDecrypted;
                bool isSignedToken = (entry.bindingMode == ReceiveSecurityHeaderBindingModes.Signed) || (entry.bindingMode == ReceiveSecurityHeaderBindingModes.SignedEndorsing);
                if (entry.MatchesId(id, encryptedForm))
                {
                    SetSigned(i);
                    if (!IsPrimaryTokenSigned)
                    {
                        IsPrimaryTokenSigned = entry.bindingMode == ReceiveSecurityHeaderBindingModes.Primary && entry.elementCategory == ReceiveSecurityHeaderElementCategory.Token;
                    }
                    return GetReader(i, encryptedForm);
                }
                else if (entry.MatchesId(id, isSignedToken))
                {
                    SetSigned(i);
                    if (!IsPrimaryTokenSigned)
                    {
                        IsPrimaryTokenSigned = entry.bindingMode == ReceiveSecurityHeaderBindingModes.Primary && entry.elementCategory == ReceiveSecurityHeaderElementCategory.Token;
                    }
                    return GetReader(i, isSignedToken);
                }
            }
            return null;
        }

        private void OnDuplicateId(string id)
        {
            throw TraceUtility.ThrowHelperError(
                new MessageSecurityException(SR.Format(SR.DuplicateIdInMessageToBeVerified, id)), securityHeader.SecurityVerifiedMessage);
        }

        public void SetBindingMode(int index, ReceiveSecurityHeaderBindingModes bindingMode)
        {
            Fx.Assert(0 <= index && index < count, "index out of range");
            elements[index].bindingMode = bindingMode;
        }

        public void SetElement(int index, object element)
        {
            Fx.Assert(0 <= index && index < count, "");
            elements[index].element = element;
        }

        public void ReplaceHeaderEntry(int index, ReceiveSecurityHeaderEntry element)
        {
            Fx.Assert(0 <= index && index < count, "");
            elements[index] = element;
        }

        public void SetElementAfterDecryption(
            int index,
            ReceiveSecurityHeaderElementCategory elementCategory, object element,
            ReceiveSecurityHeaderBindingModes bindingMode, string id, byte[] decryptedBuffer, TokenTracker supportingTokenTracker)
        {
            Fx.Assert(0 <= index && index < count, "index out of range");
            Fx.Assert(elements[index].elementCategory == ReceiveSecurityHeaderElementCategory.EncryptedData, "Replaced item must be EncryptedData");
            if (id != null)
            {
                VerifyIdUniquenessInSecurityHeader(id);
            }
            elements[index].PreserveIdBeforeDecryption();
            elements[index].SetElement(elementCategory, element, bindingMode, id, true, decryptedBuffer, supportingTokenTracker);
        }

        public void SetSignatureAfterDecryption(int index, SignedXml signedXml, byte[] decryptedBuffer)
        {
            SetElementAfterDecryption(index, ReceiveSecurityHeaderElementCategory.Signature,
                                      signedXml, ReceiveSecurityHeaderBindingModes.Unknown, signedXml.SignedInfo.Id, decryptedBuffer, null);
        }

        public void SetSignatureConfirmationAfterDecryption(int index, ISignatureValueSecurityElement signatureConfirmationElement, byte[] decryptedBuffer)
        {
            SetElementAfterDecryption(index, ReceiveSecurityHeaderElementCategory.SignatureConfirmation,
                                      signatureConfirmationElement, ReceiveSecurityHeaderBindingModes.Unknown, signatureConfirmationElement.Id, decryptedBuffer, null);
        }

        internal void SetSigned(int index)
        {
            Fx.Assert(0 <= index && index < count, "");
            elements[index].signed = true;
            if (elements[index].supportingTokenTracker != null)
            {
                elements[index].supportingTokenTracker.IsSigned = true;
            }
        }

        public void SetTimestampSigned(string id)
        {
            for (int i = 0; i < count; i++)
            {
                if (elements[i].elementCategory == ReceiveSecurityHeaderElementCategory.Timestamp &&
                    elements[i].id == id)
                {
                    SetSigned(i);
                }
            }
        }

        public void SetTokenAfterDecryption(int index, SecurityToken token, ReceiveSecurityHeaderBindingModes mode, byte[] decryptedBuffer, TokenTracker supportingTokenTracker)
        {
            SetElementAfterDecryption(index, ReceiveSecurityHeaderElementCategory.Token, token, mode, token.Id, decryptedBuffer, supportingTokenTracker);
        }

        internal bool TryGetTokenElementIndexFromStrId(string strId, out int index)
        {
            index = -1;
            SecurityKeyIdentifierClause strClause = null;
            for (int position = 0; position < Count; position++)
            {
                if (GetElementCategory(position) == ReceiveSecurityHeaderElementCategory.SecurityTokenReference)
                {
                    strClause = GetElement(position) as SecurityKeyIdentifierClause;
                    if (strClause.Id == strId)
                    {
                        break;
                    }
                }
            }

            if (strClause == null)
            {
                return false;
            }

            for (int position = 0; position < Count; position++)
            {
                if (GetElementCategory(position) == ReceiveSecurityHeaderElementCategory.Token)
                {
                    SecurityToken token = GetElement(position) as SecurityToken;
                    if (token.MatchesKeyIdentifierClause(strClause))
                    {
                        index = position;
                        return true;
                    }
                }
            }

            return false;
        }

        public void VerifyUniquenessAndSetBodyId(string id)
        {
            if (id != null)
            {
                VerifyIdUniquenessInSecurityHeader(id);
                VerifyIdUniquenessInMessageHeadersAndBody(id, headerIds.Length);
                bodyId = id;
            }
        }

        public void VerifyUniquenessAndSetBodyContentId(string id)
        {
            if (id != null)
            {
                VerifyIdUniquenessInSecurityHeader(id);
                VerifyIdUniquenessInMessageHeadersAndBody(id, headerIds.Length);
                bodyContentId = id;
            }
        }

        public void VerifyUniquenessAndSetDecryptedHeaderId(string id, int headerIndex)
        {
            if (id != null)
            {
                VerifyIdUniquenessInSecurityHeader(id);
                VerifyIdUniquenessInMessageHeadersAndBody(id, headerIndex);
                if (predecryptionHeaderIds == null)
                {
                    predecryptionHeaderIds = new string[headerIds.Length];
                }
                predecryptionHeaderIds[headerIndex] = headerIds[headerIndex];
                headerIds[headerIndex] = id;
            }
        }

        public void VerifyUniquenessAndSetHeaderId(string id, int headerIndex)
        {
            if (id != null)
            {
                VerifyIdUniquenessInSecurityHeader(id);
                VerifyIdUniquenessInMessageHeadersAndBody(id, headerIndex);
                headerIds[headerIndex] = id;
            }
        }

        private void VerifyIdUniquenessInHeaderIdTable(string id, int headerCount, string[] headerIdTable)
        {
            for (int i = 0; i < headerCount; i++)
            {
                if (headerIdTable[i] == id)
                {
                    OnDuplicateId(id);
                }
            }
        }

        private void VerifyIdUniquenessInSecurityHeader(string id)
        {
            Fx.Assert(id != null, "Uniqueness should only be tested for non-empty ids");
            for (int i = 0; i < count; i++)
            {
                if (elements[i].id == id || elements[i].encryptedFormId == id)
                {
                    OnDuplicateId(id);
                }
            }
        }

        private void VerifyIdUniquenessInMessageHeadersAndBody(string id, int headerCount)
        {
            Fx.Assert(id != null, "Uniqueness should only be tested for non-empty ids");
            VerifyIdUniquenessInHeaderIdTable(id, headerCount, headerIds);
            if (predecryptionHeaderIds != null)
            {
                VerifyIdUniquenessInHeaderIdTable(id, headerCount, predecryptionHeaderIds);
            }
            if (bodyId == id || bodyContentId == id)
            {
                OnDuplicateId(id);
            }
        }

        XmlDictionaryReader ISignatureReaderProvider.GetReader(object callbackContext)
        {
            int index = (int)callbackContext;
            Fx.Assert(index < Count, "Invalid Context provided.");
            return GetReader(index, false);
        }

        public void VerifySignatureConfirmationWasFound()
        {
            for (int i = 0; i < count; i++)
            {
                GetElementEntry(i, out ReceiveSecurityHeaderEntry entry);
                if (entry.elementCategory == ReceiveSecurityHeaderElementCategory.SignatureConfirmation)
                {
                    return;
                }
            }
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.SignatureConfirmationWasExpected));
        }
    }
}
