using System;
using CoreWCF.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace CoreWCF.IdentityModel
{
    class StandardTransformFactory : TransformFactory
    {
        static StandardTransformFactory instance = new StandardTransformFactory();

        protected StandardTransformFactory() { }

        internal static StandardTransformFactory Instance
        {
            get { return instance; }
        }

        public override Transform CreateTransform(string transformAlgorithmUri)
        {
            if (transformAlgorithmUri == SecurityAlgorithms.ExclusiveC14n)
            {
                throw new NotImplementedException();
              //  return new ExclusiveCanonicalizationTransform();
            }
            else if (transformAlgorithmUri == SecurityAlgorithms.ExclusiveC14nWithComments)
            {
                throw new NotImplementedException();

                //  return new ExclusiveCanonicalizationTransform(false, true);
            }
            else if (transformAlgorithmUri == SecurityAlgorithms.StrTransform)
            {
                return new StrTransform();
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format("UnsupportedTransformAlgorithm")));
            }
        }
    }
}
