# NamedPipeListener - Named-pipe listener and transport pipeline configuration

**Source:** `src/CoreWCF.NetNamedPipe/src/CoreWCF/Channels/NamedPipeListener.cs`
**Namespace:** `CoreWCF.Channels`
**Last validated:** 2026-11-06

## Purpose

Creates the WCF-discoverable named-pipe listener, accepts native pipe connections,
and connects each accepted stream to the framing and service-dispatch pipeline.

## Internal Structure

The constructor configures separate input/output `System.IO.Pipelines.PipeOptions`.
Output uses an inline reader scheduler and a thread-pool writer scheduler.
Its output minimum segment size is 64 KiB, keeping ordinary backing arrays below
the default LOH threshold. Its pause threshold is the greater of
256 KiB and `NamedPipeListenOptions.ConnectionBufferSize`; its resume threshold is
half that value. Input and native pipe buffers still use the configured connection
buffer (8192 bytes by default).

`Listen` publishes discovery shared memory, with global/session/AppContainer
handling. `StartAccepting` starts `MaxPendingAccepts` pumps. Each `StartPumpAsync`
creates a native server pipe, awaits connection, creates
`NamedPipeConnectionContext`, registers the connection, and starts dispatch.

`StopAsync` cancels accept pumps, disposes discovery shared memory, and closes
tracked connections.

## Design Notes

Listener `ConnectionBufferSize` and binding `MaxBufferSize` are different
settings. Raising `NetNamedPipeBinding.MaxBufferSize` does not raise output
pipeline thresholds. The segment and pause sizes do not delay flushes until full:
partial segments are visible as soon as the producer flushes.

Output segment size and backpressure interact with
`NamedPipeConnectionContext.DoSendAsync`, which writes each segment separately.
Issue #1779 motivated the larger output segments and pause threshold: they remove
most native-write amplification and permit overlap between encoding and sending.
Streaming regression tests cover prompt partial delivery, final tails, stalled
readers and bounded read-ahead. Account for per-connection memory when tuning.

## Relationships

`NetNamedPipeOptions` owns the listen options. Accepted connections use
`NamedPipeConnectionContext` and `NamedPipeConnection`; the connection delegate
ends in `NetMessageFramingConnectionHandler`.
