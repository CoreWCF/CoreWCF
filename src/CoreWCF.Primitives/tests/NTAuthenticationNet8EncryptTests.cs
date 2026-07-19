// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET8_0_OR_GREATER
using System;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using CoreWCF.Security;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    /// <summary>
    /// Regression tests for COREWCF-2026-003. NTAuthenticationNet8.Encrypt backs
    /// ISspiNegotiation.Encrypt which is used by SspiNegotiationTokenAuthenticator
    /// to wrap the SecurityContextToken proof key into the WS-Trust RSTR. The
    /// proof key MUST be transmitted with confidentiality, otherwise a passive
    /// network observer on a non-TLS leg can recover the symmetric session key
    /// and impersonate the authenticated principal for the SCT lifetime.
    ///
    /// The vulnerable code passed requestEncryption=false to
    /// NegotiateAuthentication.Wrap and ignored the isEncrypted out-parameter,
    /// causing the GSS Wrap fallback to apply only a MIC (no confidentiality)
    /// on platforms whose default GSS implementation honors the request flag.
    /// The fix passes requestEncryption=true and throws if isEncrypted comes
    /// back false. These tests reach the internal NTAuthenticationNet8 type via
    /// reflection (it has no public surface) so they do not need
    /// InternalsVisibleTo, and they require Windows because they rely on a
    /// real SSPI Negotiate handshake.
    /// </summary>
    public class NTAuthenticationNet8EncryptTests
    {
        private const string NTAuthenticationNet8TypeName =
            "CoreWCF.Security.NegotiateInternal.NTAuthenticationNet8";

        [WindowsOnlyFact]
        public void Encrypt_WrappedOutput_DoesNotContainPlaintextProofKey()
        {
            var server = CreateServerInstance();
            try
            {
                using var client = CreateLoopbackClient();

                CompleteHandshake(client, server);

                Assert.True(GetIsCompleted(server),
                    "Server-side Negotiate context did not complete handshake.");

                byte[] proofKey = new byte[32];
                RandomNumberGenerator.Fill(proofKey);

                byte[] wrapped = InvokeEncrypt(server, proofKey);

                Assert.NotNull(wrapped);
                Assert.True(wrapped.Length >= proofKey.Length,
                    $"Wrapped buffer length {wrapped.Length} is shorter than the proof key length {proofKey.Length}.");

                // The wrapped output must not contain the plaintext proof key
                // bytes verbatim. Without the fix (requestEncryption=false), on
                // platforms where GSS Wrap honors the conf_req_flag and only
                // signs, the input bytes appear in the output.
                Assert.Equal(-1, IndexOf(wrapped, proofKey));
            }
            finally
            {
                (server as IDisposable)?.Dispose();
            }
        }

        [WindowsOnlyFact]
        public void Encrypt_DoesNotThrow_WhenNegotiatedPackageSupportsConfidentiality()
        {
            var server = CreateServerInstance();
            try
            {
                using var client = CreateLoopbackClient();

                CompleteHandshake(client, server);

                byte[] proofKey = new byte[32];
                RandomNumberGenerator.Fill(proofKey);

                // Should not throw: NTLM/Kerberos negotiated on a typical
                // Windows host both support confidentiality, so the post-fix
                // isEncrypted assertion succeeds.
                Exception thrown = Record.Exception(() => InvokeEncrypt(server, proofKey));
                Assert.Null(thrown);
            }
            finally
            {
                (server as IDisposable)?.Dispose();
            }
        }

        private static object CreateServerInstance()
        {
            Type type = typeof(SecurityNegotiationException).Assembly.GetType(NTAuthenticationNet8TypeName, throwOnError: true);
            return Activator.CreateInstance(type, nonPublic: true);
        }

        private static byte[] InvokeEncrypt(object server, byte[] input)
        {
            MethodInfo method = server.GetType().GetMethod(
                "Encrypt",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(byte[]) },
                modifiers: null);
            Assert.NotNull(method);
            try
            {
                return (byte[])method.Invoke(server, new object[] { input });
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw tie.InnerException;
            }
        }

        private static bool GetIsCompleted(object server)
        {
            PropertyInfo prop = server.GetType().GetProperty("IsCompleted",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(prop);
            return (bool)prop.GetValue(server);
        }

        private static byte[] InvokeServerGetOutgoingBlob(object server, byte[] incomingBlob, out int errorCode)
        {
            MethodInfo method = server.GetType().GetMethod(
                "GetOutgoingBlob",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[]
                {
                    typeof(byte[]),
                    typeof(SecurityNegotiationException).Assembly.GetType(
                        "CoreWCF.Security.NegotiateInternal.NegotiateInternalSecurityStatusPal", throwOnError: true).MakeByRefType(),
                },
                modifiers: null);
            Assert.NotNull(method);

            object[] args = new object[] { incomingBlob, null };
            byte[] result;
            try
            {
                result = (byte[])method.Invoke(server, args);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw tie.InnerException;
            }

            object statusPal = args[1];
            FieldInfo errorCodeField = statusPal.GetType().GetField("ErrorCode",
                BindingFlags.Public | BindingFlags.Instance);
            object errorCodeValue = errorCodeField != null
                ? errorCodeField.GetValue(statusPal)
                : statusPal.GetType().GetProperty("ErrorCode", BindingFlags.Public | BindingFlags.Instance).GetValue(statusPal);
            errorCode = Convert.ToInt32(errorCodeValue);
            return result;
        }

        private static NegotiateAuthentication CreateLoopbackClient()
        {
            var clientOptions = new NegotiateAuthenticationClientOptions
            {
                Package = "Negotiate",
                Credential = CredentialCache.DefaultNetworkCredentials,
                TargetName = "HOST/" + Environment.MachineName,
                RequiredProtectionLevel = ProtectionLevel.EncryptAndSign,
            };
            return new NegotiateAuthentication(clientOptions);
        }

        private static void CompleteHandshake(NegotiateAuthentication client, object server)
        {
            // Mirror the loop in the legacy Negotiate handshake: keep alternating
            // until both sides reach Completed (or one of them errors out).
            byte[] token = null;
            NegotiateAuthenticationStatusCode clientStatus = NegotiateAuthenticationStatusCode.ContinueNeeded;

            for (int i = 0; i < 16; i++)
            {
                token = client.GetOutgoingBlob(token, out clientStatus);
                if (clientStatus != NegotiateAuthenticationStatusCode.Completed &&
                    clientStatus != NegotiateAuthenticationStatusCode.ContinueNeeded)
                {
                    throw new InvalidOperationException(
                        $"Client side Negotiate handshake failed with status: {clientStatus}");
                }

                if (token == null || token.Length == 0)
                {
                    if (clientStatus == NegotiateAuthenticationStatusCode.Completed) return;
                    continue;
                }

                token = InvokeServerGetOutgoingBlob(server, token, out int serverErrorCode);

                // 0=OK, 1=ContinueNeeded, 2=CompleteNeeded -- see
                // NegotiateInternalSecurityStatusErrorCode. Anything else is fatal.
                if (serverErrorCode != 0 && serverErrorCode != 1 && serverErrorCode != 2)
                {
                    throw new InvalidOperationException(
                        $"Server side Negotiate handshake failed with status code: {serverErrorCode}");
                }

                if (clientStatus == NegotiateAuthenticationStatusCode.Completed &&
                    (token == null || token.Length == 0))
                {
                    return;
                }
            }

            throw new InvalidOperationException("Negotiate handshake did not converge.");
        }

        private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
            {
                return -1;
            }

            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
#endif
