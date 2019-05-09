namespace Microsoft.ServiceModel.Description
{
    internal interface IContractResolver
    {
        ContractDescription ResolveContract(string contractName);
    }
}