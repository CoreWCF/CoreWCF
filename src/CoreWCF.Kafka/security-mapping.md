# ConfluentKafka
```c#
namespace Confluent.Kafka
{
    /// <summary>SecurityProtocol enum values</summary>
    public enum SecurityProtocol
    {
        /// <summary>Plaintext</summary>
        Plaintext,
        /// <summary>Ssl</summary>
        Ssl,
        /// <summary>SaslPlaintext</summary>
        SaslPlaintext,
        /// <summary>SaslSsl</summary>
        SaslSsl,
    }

    /// <summary>SaslMechanism enum values</summary>
    public enum SaslMechanism
    {
        /// <summary>GSSAPI</summary>
        Gssapi,
        /// <summary>PLAIN</summary>
        Plain,
        /// <summary>SCRAM-SHA-256</summary>
        ScramSha256,
        /// <summary>SCRAM-SHA-512</summary>
        ScramSha512,
        /// <summary>OAUTHBEARER</summary>
        OAuthBearer,
    }

}
```
# CoreWCF
```c#
namespace Confluent.Kafka
{
    public enum KafkaSecurityMode
    {
        None,
        Transport,
        TransportCredentialOnly
    }

    public enum KafkaCredentialType
    {
        None,
        SslKeyPairCertificate,
        SaslGssapi, // not supported yet because not tested with integration test.
        SaslPlain,
        SaslScramSha256, // not supported yet because not tested with integration test.
        SaslScramSha512, // not supported yet because not tested with integration test.
        SaslOAuthBearer, // not supported yet because not tested with integration test.
    }
}    
```


| SecurityProtocol | SaslMechanism | ClientCertfiicate | CoreWCF KafkaBinding configuration | 
|--|--|--|--|
| Plaintext | N/A |  N/A | KafkaSecurityMode.None + KafkaCredentialType.None 
| Ssl|  N/A | No | KafkaSecurityMode.Transport + KafkaCredentialType.None + requires configuring CaPem
| |  N/A | Yes | KafkaSecurityMode.Transport + KafkaCredentialType.SslKeyPairCertificate + required configuring CaPem + providing a SslKeyPairCredential instance
| SaslPlaintext| Gssapi|  N/A | supported through custom binding  
| | Plain|  N/A| KafkaSecurityMode.TransportCredentialOnly + KafkaCredentialType.SaslPlain +providing a SaslUsernamePasswordCredential instance
| | ScramSha256|N/A|  supported through custom binding  
| | ScramSha512|  N/A|supported through custom binding  
| | OAuthBearer|  N/A|supported through custom binding  
| SaslSsl| Gssapi | N/A|supported through custom binding 
| | Plain | N/A|KafkaSecurityMode.Transport + KafkaCredentialType.SaslPlain + requires configuring CaPem + providing a SaslUsernamePassword instance
| | ScramSha256 | N/A|supported through custom binding
| | ScramSha512 | N/A|supported through custom binding 
| | OAuthBearer | N/A|supported through custom binding 

 