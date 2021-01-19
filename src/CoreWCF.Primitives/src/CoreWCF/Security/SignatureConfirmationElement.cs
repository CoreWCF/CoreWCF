using System.Xml;
using ISignatureValueSecurityElement = CoreWCF.IdentityModel.ISignatureValueSecurityElement;
using DictionaryManager = CoreWCF.IdentityModel.DictionaryManager;

namespace CoreWCF.Security
{
    class SignatureConfirmationElement : ISignatureValueSecurityElement
    {
        SecurityVersion version;
        string id;
        byte[] signatureValue;

        public SignatureConfirmationElement(string id, byte[] signatureValue, SecurityVersion version)
        {
            if (id == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            }
            if (signatureValue == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(signatureValue));
            }
            this.id = id;
            this.signatureValue = signatureValue;
            this.version = version;
        }

        public bool HasId => true;

        public string Id => this.id;

        public byte[] GetSignatureValue()
        {
            return this.signatureValue;
        }

        public void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            this.version.WriteSignatureConfirmation(writer, this.id, this.signatureValue);
        }
    }
}
