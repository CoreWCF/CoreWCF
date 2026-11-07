# NamedPipeConnectionContext - Native named-pipe and pipeline transport bridge

**Source:** `src/CoreWCF.NetNamedPipe/src/CoreWCF/Channels/NamedPipeConnectionContext.cs`
**Namespace:** `CoreWCF.Channels`
**Last validated:** 2026-11-06

## Purpose

Bridges a `NamedPipeServerStream` and paired pipelines exposed to framing as a
`DefaultConnectionContext`. Implements asynchronous receive/send loops and
connection shutdown.

## Key API

`Start` launches `_receivingTask` and `_sendingTask`.
`Input` is `Application.Output`, the writer used by the receive loop.
`Output` is `Application.Input`, the reader consumed by the send loop.

## Internal Structure

The constructor creates input/output pipes using options supplied by
`NamedPipeListener` and builds complementary `Transport`/`Application` duplex
views.

`DoReceiveAsync` gets pipeline memory, reads from the native pipe, advances the
writer, and awaits its flush/backpressure. Non-array memory uses `_readBuffer`.

`DoSendAsync` awaits `Output.ReadAsync`. A single-segment buffer goes directly
to `WriteAsync`; multi-segment buffers are enumerated and each segment is
separately awaited. Only after the entire buffer has been written does it call
`Output.AdvanceTo(buffer.End)`, releasing producer backpressure.

`WriteAsync(ReadOnlyMemory<byte>)` uses the array-backed fast path when possible.
Otherwise it copies through `_writeBuffer` in connection-buffer-sized pieces.
The common array-backed path still issues one native async write per segment;
it does not coalesce segments or use scatter/gather I/O.

## Design Notes

Small pipeline segments amplify native writes and asynchronous completions.
Small pause thresholds make the streamed producer wait for the consumer to drain
each large chunk. Both effects matter together; do not assume framing write size
equals native pipe write size.

Issue #1779 diagnostics observed approximately 20,167 native writes per 70 MiB
response, mostly 4096-byte segments, with pre-fix default settings on .NET 10.
`NamedPipeListener` now configures larger output segments and thresholds to avoid
that amplification. This count is historical workload evidence, not an invariant.

Shutdown is guarded by `_shutdownLock`, disposes/disconnects the native transport,
completes pipelines, and signals connection-closed cancellation. Send-loop
completion and cancellation must not strand a producer awaiting a flush.

## Relationships

Created by `NamedPipeListener.StartPumpAsync`; owned by `NamedPipeConnection`.
The framing layer produces output through the complementary transport writer.
