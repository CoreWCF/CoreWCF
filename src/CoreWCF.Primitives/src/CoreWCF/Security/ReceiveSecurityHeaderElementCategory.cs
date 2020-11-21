namespace CoreWCF.Security
{
    enum ReceiveSecurityHeaderElementCategory
    {
        Signature,
        EncryptedData,
        EncryptedKey,
        SignatureConfirmation,
        ReferenceList,
        SecurityTokenReference,
        Timestamp,
        Token
    }
}
