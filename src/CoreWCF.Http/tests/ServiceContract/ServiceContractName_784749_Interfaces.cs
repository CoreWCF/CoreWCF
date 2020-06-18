using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Name = "<hello>\"'")]
    public interface IServiceContractName_784749_XmlCharacters_Service
    {
        [OperationContract]
        string Method1(string input);
    }

    [ServiceContract(Name = "   \t \t \n ")]
    public interface IServiceContractName_784749_WhiteSpace_Service
    {
        [OperationContract(Action = "WhiteSpaceInName")]
        string Method2(string input);
    }

    [ServiceContract(Name = "&lt;&gt;&gt;")]
    public interface IServiceContractName_784749_XMLEncoded_Service
    {
        [OperationContract]
        string Method3(string input);
    }

    [ServiceContract(Name = "http://y.c/5")]
    public interface IServiceContractName_784749_NonAlphaCharacters_Service
    {
        [OperationContract]
        string Method4(string input);
    }

    [ServiceContract(Name = "İüğmeIiiçeI")]
    public interface IServiceContractName_784749_LocalizedCharacters_Service
    {
        [OperationContract(Action = "localizedCharsInName")]
        string Method5(string input);
    }

    [ServiceContract(Name = "esÅeoplÀÁð")]
    public interface IServiceContractName_784749_SurrogateCharacters_Service
    {
        [OperationContract]
        string Method6(string input);
    }

    [ServiceContract(Name = "http://hello/\0")]
    public interface IServiceContractName_784749_XMLReservedCharacters_Service
    {
        [OperationContract(Action = "xmlReservedCharInName")]
        string Method7(string input);
    }

    [ServiceContract(Name = "http://www.yahoo.com")]
    public interface IServiceContractName_784749_URI_Service
    {
        [OperationContract]
        string Method8(string input);
    }
}