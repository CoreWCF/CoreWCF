using System;
using System.Security.Authentication.ExtendedProtection;

namespace CoreWCF.Security
{

    internal interface ISspiNegotiation : IDisposable
  {
   // DateTime ExpirationTimeUtc { get; }

    bool IsCompleted { get; }

    bool IsValidContext { get; }

    string KeyEncryptionAlgorithm { get; }

    byte[] Decrypt(byte[] encryptedData);

    byte[] Encrypt(byte[] data);

    byte[] GetOutgoingBlob(
      byte[] incomingBlob,
      ChannelBinding channelbinding,
      ExtendedProtectionPolicy protectionPolicy);

    string GetRemoteIdentityName();
  }
    interface ISspiNegotiationInfo
    {
        ISspiNegotiation SspiNegotiation { get; }
    }
}
