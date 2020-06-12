using CoreWCF;

namespace ServiceContract
{
    [ServiceContract(Namespace = "<hello>\"\'")]
    public interface IServiceContractNamespace_784756_XmlCharacters_Service
    {
        [OperationContract]
        string Method1(string input);
    }

    [ServiceContract(Namespace = "   \t \t \n ")]
    public interface IServiceContractNamespace_784756_WhiteSpace_Service
    {
        [OperationContract(Action = "WhiteSpaceInNamespace")]
        string Method2(string input);
    }

    [ServiceContract(Namespace = "&lt;&gt;&gt;")]
    public interface IServiceContractNamespace_784756_XMLEncoded_Service
    {
        [OperationContract]
        string Method3(string input);
    }

    [ServiceContract(Namespace = "http://y.c/5")]
    public interface IServiceContractNamespace_784756_NonAlphaCharacters_Service
    {
        [OperationContract]
        string Method4(string input);
    }

    [ServiceContract(Namespace = "İüğmeIiiçeI")]
    public interface IServiceContractNamespace_784756_LocalizedCharacters_Service
    {
        [OperationContract(Action = "LocalizedCharInNamespace")]
        string Method5(string input);
    }

    [ServiceContract(Namespace = "esÅeoplÀÁð")]
    public interface IServiceContractNamespace_784756_SurrogateCharacters_Service
    {
        [OperationContract]
        string Method6(string input);
    }

    [ServiceContract(Namespace = "http://hello/\0")]
    public interface IServiceContractNamespace_784756_XMLReservedCharacters_Service
    {
        [OperationContract(Action = "XmlReservedCharInNamespace")]
        string Method7(string input);
    }

    [ServiceContract(Namespace = "http://www.yahoo.com")]
    public interface IServiceContractNamespace_784756_URI_Service
    {
        [OperationContract]
        string Method8(string input);
    }
}