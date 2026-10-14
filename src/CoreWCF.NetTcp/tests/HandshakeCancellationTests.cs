// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Channels.Framing;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoreWCF.NetTcp.Tests
{
    public class HandshakeCancellationTests
    {
        private static readonly Assembly s_framingAssembly = typeof(FramingConnection).Assembly;

        [Theory]
        [InlineData(false, "read")]
        [InlineData(true, "read")]
        [InlineData(false, "upgrade-write")]
        [InlineData(true, "upgrade-write")]
        [InlineData(false, "upgrade-flush")]
        [InlineData(true, "upgrade-flush")]
        [InlineData(false, "ack-write")]
        [InlineData(true, "ack-write")]
        [InlineData(false, "ack-flush")]
        [InlineData(true, "ack-flush")]
        public async Task PreambleIoUsesInitializationCancellation(bool singleton, string operation)
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton);
            test.Writer.BlockOperation = operation;
            SetConnectionProperty(test.Connection, "StreamUpgradeAcceptor", new TestUpgradeAcceptor());
            if (operation.StartsWith("upgrade", StringComparison.Ordinal))
            {
                await test.SendAsync(UpgradeRequest("application/test"));
            }
            else if (operation.StartsWith("ack", StringComparison.Ordinal))
            {
                await test.SendAsync(new byte[] { 0x0c });
            }

            bool dispatched = false;
            Task handshake = InvokeHandshake(test.Connection, singleton, connection =>
            {
                dispatched = true;
                return Task.CompletedTask;
            });
            await WithinAsync(operation == "read" ? test.Reader.PendingRead.Task : test.Writer.PendingOperation.Task);
            initialization.Cancel();

            await WithinAsync(handshake, expectCancellation: true);
            Assert.False(dispatched);
            Assert.True(test.Context.Aborted);
            if (operation == "read")
            {
                Assert.Equal(initialization.Token, test.Reader.LastToken);
            }
            else
            {
                Assert.Equal(initialization.Token, test.Writer.LastToken);
            }
        }

        [Theory]
        [InlineData(false, "fault-write")]
        [InlineData(true, "fault-write")]
        [InlineData(false, "fault-flush")]
        [InlineData(true, "fault-flush")]
        [InlineData(false, "fault-drain")]
        [InlineData(true, "fault-drain")]
        public async Task InvalidSingletonUpgradeUsesInitializationDeadline(bool unsupported, string operation)
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton: true);
            test.Writer.BlockOperation = operation;
            if (unsupported)
            {
                SetConnectionProperty(test.Connection, "StreamUpgradeAcceptor",
                    new TestUpgradeAcceptor { SupportsUpgrade = false });
            }
            await test.SendAsync(UpgradeRequest("application/test"));
            Task handshake = InvokeHandshake(test.Connection, singleton: true, connection => Task.CompletedTask);
            await WithinAsync(operation == "fault-drain" ? test.Reader.PendingRead.Task : test.Writer.PendingOperation.Task);
            initialization.Cancel();

            Assert.Same(handshake, await Task.WhenAny(handshake, Task.Delay(TimeSpan.FromSeconds(5))));
            await Assert.ThrowsAsync<ProtocolException>(() => handshake);
            Assert.True(test.Context.Aborted);
            Assert.Equal(initialization.Token, operation == "fault-drain" ? test.Reader.LastToken : test.Writer.LastToken);
        }

        [Fact]
        public async Task FaultFlushPreservesTheCallerToken()
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using CancellationTokenSource send = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton: false);
            test.Writer.BlockOperation = "fault-flush";
            Func<string, int, CancellationToken, Task> sendFault =
                GetMethod<Func<string, int, CancellationToken, Task>>(
                    typeof(FramingConnection), "SendFaultAsync", test.Connection);
            Task fault = sendFault("urn:test:fault", 1024, send.Token);
            await WithinAsync(test.Writer.PendingOperation.Task);
            send.Cancel();
            await WithinAsync(fault);
            Assert.True(test.Context.Aborted);
            Assert.Equal(send.Token, test.Writer.LastToken);
            Assert.False(initialization.IsCancellationRequested);
        }

        [Theory]
        [InlineData("read")]
        [InlineData("sync-read")]
        [InlineData("apm-read")]
        [InlineData("write")]
        [InlineData("sync-write")]
        [InlineData("apm-write")]
        [InlineData("flush")]
        [InlineData("sync-flush")]
        public async Task UpgradeAdapterAppliesDeadlineToLegacyIo(string operation)
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton: false);
            test.Writer.BlockOperation = operation.Contains("write") ? "adapter-write" : "adapter-flush";
            Stream stream = CreateUpgradeStream(test.Connection);
            byte[] buffer = new byte[1];
            Task transfer;
            switch (operation)
            {
                case "read":
                    transfer = stream.ReadAsync(buffer, 0, 1, CancellationToken.None);
                    break;
                case "sync-read":
                    transfer = Task.Run(() => stream.Read(buffer, 0, 1));
                    break;
                case "apm-read":
                    transfer = Task.Run(() => stream.EndRead(stream.BeginRead(buffer, 0, 1, null, null)));
                    break;
                case "write":
                    transfer = stream.WriteAsync(buffer, 0, 1, CancellationToken.None);
                    break;
                case "sync-write":
                    transfer = Task.Run(() => stream.Write(buffer, 0, 1));
                    break;
                case "apm-write":
                    transfer = Task.Run(() => stream.EndWrite(stream.BeginWrite(buffer, 0, 1, null, null)));
                    break;
                case "flush":
                    transfer = stream.FlushAsync(CancellationToken.None);
                    break;
                default:
                    transfer = Task.Run(() => stream.Flush());
                    break;
            }
            await WithinAsync(operation.Contains("read") ? test.Reader.PendingRead.Task : test.Writer.PendingOperation.Task);
            initialization.Cancel();
            await WithinAsync(transfer, expectCancellation: true);
        }

        [Fact]
        public async Task UpgradeAdapterPreservesIndependentCallerCancellation()
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using CancellationTokenSource caller = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton: false);
            Stream stream = CreateUpgradeStream(test.Connection);
            Task read = stream.ReadAsync(new byte[1], 0, 1, caller.Token);
            await WithinAsync(test.Reader.PendingRead.Task);
            caller.Cancel();
            await WithinAsync(read, expectCancellation: true);
            Assert.False(initialization.IsCancellationRequested);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializationScopeEndsBeforeEstablishedDispatch(bool singleton)
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton);
            await test.SendAsync(new byte[] { 0x0c });
            Stream stream = CreateUpgradeStream(test.Connection);
            TaskCompletionSource<bool> enteredDispatch = CompletionSource();
            TaskCompletionSource<bool> releaseDispatch = CompletionSource();
            Task handshake = InvokeHandshake(test.Connection, singleton, async connection =>
            {
                enteredDispatch.SetResult(true);
                await releaseDispatch.Task;
            });
            await WithinAsync(enteredDispatch.Task);
            initialization.Cancel();
            Assert.False(test.Context.Aborted);

            await test.SendAsync(new byte[] { 0x42 });
            byte[] data = new byte[1];
            Task<int> read = stream.ReadAsync(data, 0, 1, CancellationToken.None);
            Assert.Same(read, await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5))));
            Assert.Equal(1, await read);
            Assert.Equal(0x42, data[0]);
            releaseDispatch.SetResult(true);
            await WithinAsync(handshake);
        }

        [Fact]
        public async Task OldUpgradeAdapterDoesNotAdoptReusedConnectionDeadline()
        {
            using CancellationTokenSource first = new CancellationTokenSource();
            using CancellationTokenSource reused = new CancellationTokenSource();
            using TestConnection test = new TestConnection(first.Token, singleton: false);
            Stream oldStream = CreateUpgradeStream(test.Connection);
            GetMethod<Action>(typeof(FramingConnection), "CompleteInitialization", test.Connection)();
            GetMethod<Action>(typeof(FramingConnection), "Reset", test.Connection)();
            SetConnectionProperty(test.Connection, "ChannelInitializationCancellationToken", reused.Token);
            reused.Cancel();
            await test.SendAsync(new byte[] { 0x43 });
            byte[] data = new byte[1];
            Task<int> read = oldStream.ReadAsync(data, 0, 1, CancellationToken.None);
            Assert.Same(read, await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5))));
            Assert.Equal(1, await read);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NestedUpgradeReadCannotEscapeInitializationDeadline(bool singleton)
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton);
            TestUpgradeAcceptor acceptor = new TestUpgradeAcceptor { BlockOnUpgrade = 2 };
            SetConnectionProperty(test.Connection, "StreamUpgradeAcceptor", acceptor);
            byte[] request = UpgradeRequest("application/test");
            byte[] requests = new byte[request.Length * 2];
            Buffer.BlockCopy(request, 0, requests, 0, request.Length);
            Buffer.BlockCopy(request, 0, requests, request.Length, request.Length);
            await test.SendAsync(requests);
            Task handshake = InvokeHandshake(test.Connection, singleton, connection => Task.CompletedTask);
            await WithinAsync(acceptor.BlockingUpgrade.Task);
            initialization.Cancel();
            await WithinAsync(handshake, expectCancellation: true);
            Assert.Equal(2, acceptor.UpgradeCount);
            Assert.True(test.Context.Aborted);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CompletedNestedUpgradesDoNotCancelEstablishedTraffic(bool singleton)
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton);
            TestUpgradeAcceptor acceptor = new TestUpgradeAcceptor();
            SetConnectionProperty(test.Connection, "StreamUpgradeAcceptor", acceptor);
            byte[] request = UpgradeRequest("application/test");
            byte[] preamble = new byte[request.Length * 2 + 1];
            Buffer.BlockCopy(request, 0, preamble, 0, request.Length);
            Buffer.BlockCopy(request, 0, preamble, request.Length, request.Length);
            preamble[preamble.Length - 1] = 0x0c;
            await test.SendAsync(preamble);
            TaskCompletionSource<bool> enteredDispatch = CompletionSource();
            TaskCompletionSource<bool> releaseDispatch = CompletionSource();
            Task handshake = InvokeHandshake(test.Connection, singleton, async connection =>
            {
                enteredDispatch.SetResult(true);
                await releaseDispatch.Task;
            });
            await WithinAsync(enteredDispatch.Task);
            initialization.Cancel();
            Assert.False(test.Context.Aborted);
            Assert.Equal(2, acceptor.UpgradeCount);
            await test.SendAsync(new byte[] { 0x44 });
            Task<ReadResult> read = test.Connection.Input.ReadAsync(CancellationToken.None).AsTask();
            Assert.Same(read, await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5))));
            ReadResult result = await read;
            Assert.Equal(0x44, Assert.Single(result.Buffer.ToArray()));
            test.Connection.Input.AdvanceTo(result.Buffer.End);
            releaseDispatch.SetResult(true);
            await WithinAsync(handshake);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LegacyTlsAuthenticationHonorsInitializationDeadline(bool singleton)
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton);
            using RSA rsa = RSA.Create(2048);
            CertificateRequest certificateRequest = new CertificateRequest(
                "CN=HandshakeCancellationTests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = certificateRequest.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            TestUpgradeAcceptor acceptor = new TestUpgradeAcceptor { TlsCertificate = certificate };
            SetConnectionProperty(test.Connection, "StreamUpgradeAcceptor", acceptor);
            await test.SendAsync(UpgradeRequest("application/test"));
            Task handshake = InvokeHandshake(test.Connection, singleton, connection => Task.CompletedTask);
            await WithinAsync(acceptor.BlockingUpgrade.Task);
            initialization.Cancel();
            await AssertNegotiationCanceledAsync(handshake);
            Assert.True(test.Context.Aborted);
        }

        [WindowsOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LegacyNegotiateAuthenticationHonorsInitializationDeadline(bool singleton)
        {
            using CancellationTokenSource initialization = new CancellationTokenSource();
            using TestConnection test = new TestConnection(initialization.Token, singleton);
            TestUpgradeAcceptor acceptor = new TestUpgradeAcceptor { Negotiate = true };
            SetConnectionProperty(test.Connection, "StreamUpgradeAcceptor", acceptor);
            await test.SendAsync(UpgradeRequest("application/test"));
            Task handshake = InvokeHandshake(test.Connection, singleton, connection => Task.CompletedTask);
            await WithinAsync(acceptor.BlockingUpgrade.Task);
            initialization.Cancel();
            await AssertNegotiationCanceledAsync(handshake);
            Assert.True(test.Context.Aborted);
        }

        private static async Task AssertNegotiationCanceledAsync(Task task)
        {
            Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))));
            Exception exception = await Record.ExceptionAsync(() => task);
            Assert.True(exception is OperationCanceledException || exception is IOException
                || exception is AuthenticationException, exception?.ToString());
        }

        private static TaskCompletionSource<bool> CompletionSource()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task WithinAsync(Task task, bool expectCancellation = false)
        {
            Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))));
            if (expectCancellation)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            }
            else
            {
                await task;
            }
        }

        private static byte[] UpgradeRequest(string protocol)
        {
            byte[] encoded = Encoding.UTF8.GetBytes(protocol);
            byte[] request = new byte[encoded.Length + 2];
            request[0] = 0x09;
            request[1] = checked((byte)encoded.Length);
            Buffer.BlockCopy(encoded, 0, request, 2, encoded.Length);
            return request;
        }

        private static Stream CreateUpgradeStream(FramingConnection connection)
        {
            Type type = s_framingAssembly.GetType("CoreWCF.Channels.Framing.DuplexPipeStream", throwOnError: true);
            return (Stream)Activator.CreateInstance(type, connection.Input, connection.Output, connection);
        }

        private static Task InvokeHandshake(FramingConnection connection, bool singleton, HandshakeDelegate next)
        {
            string name = singleton ? "ServerFramingSingletonMiddleware" : "ServerFramingDuplexSessionMiddleware";
            Type type = s_framingAssembly.GetType("CoreWCF.Channels.Framing." + name, throwOnError: true);
            object middleware = Activator.CreateInstance(type, next);
            return GetMethod<Func<FramingConnection, Task>>(type, "OnConnectedAsync", middleware)(connection);
        }

        private static T GetMethod<T>(Type type, string name, object instance) where T : Delegate
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)method.CreateDelegate(typeof(T), instance);
        }

        private static void SetConnectionProperty(FramingConnection connection, string name, object value)
        {
            typeof(FramingConnection).GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(connection, value);
        }

        private sealed class TestConnection : IDisposable
        {
            private readonly Pipe _input = new Pipe();

            public TestConnection(CancellationToken initialization, bool singleton)
            {
                Reader = new RecordingReader(_input.Reader);
                Writer = new RecordingWriter();
                Context = new RecordingContext { Transport = new TestPipe(Reader, Writer) };
                Connection = new FramingConnection(Context);
                SetConnectionProperty(Connection, "ChannelInitializationCancellationToken", initialization);
                SetConnectionProperty(Connection, "MessageEncoderFactory",
                    new BinaryMessageEncodingBindingElement().CreateMessageEncoderFactory());
                SetConnectionProperty(Connection, "ServiceDispatcher", new TestDispatcher());
                string decoderName = singleton ? "ServerSingletonDecoder" : "ServerSessionDecoder";
                Type decoderType = s_framingAssembly.GetType(
                    "CoreWCF.Channels.Framing." + decoderName, throwOnError: true);
                object decoder = Activator.CreateInstance(decoderType, 1024, 1024, NullLogger.Instance);
                Func<ReadOnlySequence<byte>, int> decode =
                    GetMethod<Func<ReadOnlySequence<byte>, int>>(decoderType, "Decode", decoder);
                byte[] via = Encoding.UTF8.GetBytes("net.tcp://localhost/test");
                byte[] preamble = new byte[via.Length + 4];
                preamble[0] = 0x02;
                preamble[1] = checked((byte)via.Length);
                Buffer.BlockCopy(via, 0, preamble, 2, via.Length);
                preamble[preamble.Length - 2] = 0x03;
                preamble[preamble.Length - 1] = singleton ? (byte)0x07 : (byte)0x08;
                ReadOnlySequence<byte> buffer = new ReadOnlySequence<byte>(preamble);
                while (!buffer.IsEmpty)
                {
                    buffer = buffer.Slice(decode(buffer));
                }
                SetConnectionProperty(Connection, "FramingDecoder", decoder);
            }

            public FramingConnection Connection { get; }
            public RecordingContext Context { get; }
            public RecordingReader Reader { get; }
            public RecordingWriter Writer { get; }

            public Task SendAsync(byte[] data) => _input.Writer.WriteAsync(data).AsTask();

            public void Dispose()
            {
                _input.Writer.Complete();
                _input.Reader.Complete();
            }
        }

        private sealed class TestDispatcher : IServiceDispatcher
        {
            public Uri BaseAddress => new Uri("net.tcp://localhost/test");
            public Binding Binding { get; } = new NetTcpBinding(SecurityMode.None)
            {
                ReceiveTimeout = TimeSpan.FromMinutes(10)
            };
            public ServiceHostBase Host => null;
            public IList<Type> SupportedChannelTypes => Array.Empty<Type>();
            public Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
                => throw new NotSupportedException();
        }

        private sealed class RecordingContext : DefaultConnectionContext
        {
            private int _aborted;
            public bool Aborted => Volatile.Read(ref _aborted) != 0;
            public override void Abort(ConnectionAbortedException abortReason)
                => Interlocked.Exchange(ref _aborted, 1);
        }

        private sealed class TestPipe : IDuplexPipe
        {
            public TestPipe(PipeReader input, PipeWriter output)
            {
                Input = input;
                Output = output;
            }
            public PipeReader Input { get; }
            public PipeWriter Output { get; }
        }

        private sealed class RecordingReader : PipeReader
        {
            private readonly PipeReader _inner;
            public RecordingReader(PipeReader inner) => _inner = inner;
            public CancellationToken LastToken { get; private set; }
            public TaskCompletionSource<bool> PendingRead { get; } = CompletionSource();
            public override void AdvanceTo(SequencePosition consumed) => _inner.AdvanceTo(consumed);
            public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
                => _inner.AdvanceTo(consumed, examined);
            public override void CancelPendingRead() => _inner.CancelPendingRead();
            public override void Complete(Exception exception = null) => _inner.Complete(exception);
            public override bool TryRead(out ReadResult result) => _inner.TryRead(out result);
            public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
            {
                LastToken = cancellationToken;
                ValueTask<ReadResult> read = _inner.ReadAsync(cancellationToken);
                if (!read.IsCompleted)
                {
                    PendingRead.TrySetResult(true);
                }
                return read;
            }
        }

        private sealed class RecordingWriter : PipeWriter
        {
            private readonly byte[] _memory = new byte[4096];
            private string _lastRecord = "adapter";
            public string BlockOperation { get; set; }
            public CancellationToken LastToken { get; private set; }
            public TaskCompletionSource<bool> PendingOperation { get; } = CompletionSource();
            public override void Advance(int bytes) { }
            public override void CancelPendingFlush() { }
            public override void Complete(Exception exception = null) { }
            public override Memory<byte> GetMemory(int sizeHint = 0) => _memory;
            public override Span<byte> GetSpan(int sizeHint = 0) => _memory;
            public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source,
                CancellationToken cancellationToken = default)
            {
                LastToken = cancellationToken;
                byte record = source.IsEmpty ? (byte)0 : source.Span[0];
                _lastRecord = record == 0x0a ? "upgrade" : record == 0x0b ? "ack" : record == 0x08 ? "fault" : "adapter";
                return BlockOperation == _lastRecord + "-write"
                    ? new ValueTask<FlushResult>(WaitForCancellationAsync(cancellationToken))
                    : new ValueTask<FlushResult>(new FlushResult(false, false));
            }
            public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
            {
                LastToken = cancellationToken;
                return BlockOperation == _lastRecord + "-flush"
                    ? new ValueTask<FlushResult>(WaitForCancellationAsync(cancellationToken))
                    : new ValueTask<FlushResult>(new FlushResult(false, false));
            }
            private async Task<FlushResult> WaitForCancellationAsync(CancellationToken token)
            {
                TaskCompletionSource<FlushResult> completion = new TaskCompletionSource<FlushResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using (token.Register(() => completion.TrySetCanceled()))
                {
                    PendingOperation.TrySetResult(true);
                    return await completion.Task;
                }
            }
        }

        private sealed class TestUpgradeAcceptor : StreamUpgradeAcceptor
        {
            public bool SupportsUpgrade { get; set; } = true;
            public int BlockOnUpgrade { get; set; }
            public int UpgradeCount { get; private set; }
            public X509Certificate2 TlsCertificate { get; set; }
            public bool Negotiate { get; set; }
            public TaskCompletionSource<bool> BlockingUpgrade { get; } = CompletionSource();
            public override bool CanUpgrade(string contentType) => SupportsUpgrade;
            public override async Task<Stream> AcceptUpgradeAsync(Stream stream)
            {
                UpgradeCount++;
                if (TlsCertificate != null)
                {
                    SslStream ssl = new SslStream(stream, leaveInnerStreamOpen: true);
                    BlockingUpgrade.TrySetResult(true);
                    await ssl.AuthenticateAsServerAsync(TlsCertificate, false, SslProtocols.Tls12, false);
                    return ssl;
                }
                if (Negotiate)
                {
                    NegotiateStream negotiate = new NegotiateStream(stream, leaveInnerStreamOpen: true);
                    BlockingUpgrade.TrySetResult(true);
                    await negotiate.AuthenticateAsServerAsync(CredentialCache.DefaultNetworkCredentials,
                        ProtectionLevel.EncryptAndSign, TokenImpersonationLevel.Identification);
                    return negotiate;
                }
                if (BlockOnUpgrade == UpgradeCount)
                {
                    BlockingUpgrade.TrySetResult(true);
                    await stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None);
                }
                return stream;
            }
        }
    }
}
