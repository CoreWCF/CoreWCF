namespace CoreWCF.IdentityModel
{
    internal interface ISignatureValueSecurityElement : ISecurityElement
    {
        byte[] GetSignatureValue();
    }
}
