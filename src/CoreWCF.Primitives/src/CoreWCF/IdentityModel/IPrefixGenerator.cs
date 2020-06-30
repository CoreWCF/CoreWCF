
namespace CoreWCF.IdentityModel
{
    interface IPrefixGenerator
    {
        string GetPrefix(string namespaceUri, int depth, bool isForAttribute);
    }
}
