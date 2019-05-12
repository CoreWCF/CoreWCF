namespace CoreWCF.Description
{
    internal interface IContractResolver
    {
        ContractDescription ResolveContract(string contractName);
    }
}