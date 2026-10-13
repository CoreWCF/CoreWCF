# AppContext switches

This document describes CoreWCF AppContext switches. Document future switches here, including their default value, behavior, and any compatibility or security implications.

## `CoreWCF.Security.AllowNonCertificateX509Data`

**Default:** `false` (disabled).

By default, CoreWCF requires certificate content (`X509ContentType.Cert`) when reading raw X.509 data in security tokens, key identifiers, SAML signatures, endpoint identities, and secure-conversation claim sets. PKCS#12/PFX, PKCS#7, and Windows serialized certificate/store data are rejected before import. Windows serialized certificates can contain unsigned native properties that affect certificate-chain trust.

Applications that temporarily need the legacy format handling can opt out before using CoreWCF:

```csharp
AppContext.SetSwitch("CoreWCF.Security.AllowNonCertificateX509Data", true);
```

Enabling this switch restores legacy format handling and removes this protection; prefer sending ordinary certificate data instead. Formats that the runtime cannot import remain unsupported. No registry setting is used, and APIs that accept an existing `X509Certificate2` are unchanged.
