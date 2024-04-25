// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel
{
    internal class InternalReferenceWrapper
    {
        private readonly Reference _reference;
        internal bool _verified;
        private string _referredId;
        private SignatureResourcePool _resourcePool;

        public string DigestMethod => _reference.DigestMethod;

        public string Id => _reference.Id;

        public InternalReferenceWrapper(Reference signInfoReference, SignatureResourcePool resourcePool)
        {
            _reference = signInfoReference;
            _resourcePool = resourcePool;
        }
        internal void EnsureDigestValidity(string id, byte[] computedDigest)
        {
            if (!EnsureDigestValidityIfIdMatches(id, computedDigest))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(
                    SR.Format("RequiredTargetNotSigned", id))); //TODO message
            }
        }

        private void EnsureDigestValidity(string id, XmlReader resolvedXmlSource)
        {
            if (!EnsureDigestValidityIfIdMatches(id, resolvedXmlSource))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(
                    SR.Format("RequiredTargetNotSigned", id))); //TODO
            }
        }

        private void EnsureDigestValidity(string id, ISecurityElement resolvedXmlSource)
        {
           throw new NotImplementedException();
        }

        internal void EnsureDigestValidity(string id, object input)
        {
            XmlReader reader = input as XmlReader;

            if (reader != null)
            {
                EnsureDigestValidity(id,reader);
            }
            else if (input is ISecurityElement)
            {
                EnsureDigestValidity(id, input);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        internal bool IsStrTranform() =>  (_reference.TransformChain.Count == 1 && _reference.TransformChain[0].Algorithm == SecurityAlgorithms.StrTransform);

        internal bool EnsureDigestValidityIfIdMatches(string id, byte[] computedDigest)
        {
            if (_verified || id != ExtractReferredId())
            {
                return false;
            }
            if (!CryptoHelper.FixedTimeEquals(computedDigest, _reference.DigestValue))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new CryptographicException(SR.Format("DigestVerificationFailedForReference", _reference.Uri)));
            }
            _verified = true;
            return true;
        }

        internal bool EnsureDigestValidityIfIdMatches(string id, object resolvedXmlSource)
        {
            if (_verified)
            {
                return false;
            }

            // During StrTransform the extractedReferredId on the reference will point to STR and hence will not be 
            // equal to the referred element ie security token Id.
            if (id != ExtractReferredId() && !IsStrTranform())
            {
                return false;
            }

            if (!CheckDigest(resolvedXmlSource))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new CryptographicException(SR.Format("DigestVerificationFailedForReference", _reference.Uri))); //TODO
            }
            _verified = true;
            return true;
        }

        private bool CheckDigest(object resolvedXmlSource)
        {
            byte[] digValue = _reference.DigestValue;
            if(_reference.TransformChain.Count>1 || resolvedXmlSource is not XmlReader)
            {
                throw new NotSupportedException();
            }
            CanonicalizationDriver driver = GetConfiguredDriver(_resourcePool);
            driver.SetInput(resolvedXmlSource as XmlReader);
            var data = driver.GetMemoryStream();
            Transform chain = _reference.TransformChain[0];
            chain.LoadInput(data);
            byte[] computedDigest = chain.GetDigestedOutput(_resourcePool.TakeHashAlgorithm(_reference.DigestMethod));
            return CryptoHelper.FixedTimeEquals(computedDigest, digValue);
        }

        internal string ExtractReferredId()
        {
            if (_referredId == null)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(_reference.Uri, string.Empty))
                {
                    return string.Empty;
                }

                if (_reference.Uri == null || _reference.Uri.Length < 2 || _reference.Uri[0] != '#')
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new CryptographicException(SR.Format(SR.UnableToResolveReferenceUriForSignature,  _reference.Uri)));
                }
                _referredId = _reference.Uri.Substring(1);
            }
            return _referredId;
        }

        private CanonicalizationDriver GetConfiguredDriver(SignatureResourcePool resourcePool)
        {
            CanonicalizationDriver driver = resourcePool.TakeCanonicalizationDriver();
           // driver.IncludeComments = this.IncludeComments;
           // driver.SetInclusivePrefixes(this.inclusivePrefixes);
            return driver;
        }
        internal bool NeedsInclusiveContext() => false;
        /*
private byte[] GetDigestValue() => TransformChain[0]
private string ExtractReferredId() => throw new NotImplementedException();

private bool NeedsInclusiveContext() => throw new NotImplementedException();


private bool CheckDigest(object resolvedXmlSource)
{
this.TransformChain[0].LoadInput(resolvedXmlSource);
byte[] computedDigest = this.TransformChain[0].GetDigestedOutput(this.DigestMethod);
byte[] computedDigest = ComputeDigest();
bool result = CryptoHelper.FixedTimeEquals(computedDigest, DigestValue);
return result;
}

public void ComputeAndSetDigest()
{
digestValueElement = ComputeDigest();
}

public byte[] ComputeDigest()
{
if (this.TransformChain.Count == 0)
{
throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.GetString(SR.EmptyTransformChainNotSupported)));
}
this.d

if (this.resolvedXmlSource == null)
{
throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(
SR.GetString(SR.UnableToResolveReferenceUriForSignature, this.uri)));
}
return this.TransformChain[0]. .TransformToDigest(this.resolvedXmlSource, this.ResourcePool, this.DigestMethod, this.dictionaryManager);
}*/
    }

}
