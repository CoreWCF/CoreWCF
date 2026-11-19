# StreamedFramingRequestContext - Streamed singleton framing request and reply

**Source:** `src/CoreWCF.NetFramingBase/src/CoreWCF/Channels/Framing/StreamedFramingRequestContext.cs`
**Namespace:** `CoreWCF.Channels.Framing`
**Last validated:** 2026-11-06

## Purpose

Connects request dispatch to streamed singleton framing. This file also contains
`StreamingConnectionHelper` and the framing-aware output stream passed to message
encoders.

## Internal Structure

`OnReplyAsync` applies compression properties when appropriate, then delegates
to `StreamingConnectionHelper.WriteMessageAsync`. For streamed output the helper
writes the envelope start, passes a `StreamingOutputConnectionStream` to the
encoder, and writes envelope-end bytes afterward.

`OnCloseAsync` drains unread request input, writes framing end bytes, handles raw
stream cleanup, and completes `ReplySent`. Abort also completes the task source
and aborts the connection.

`StreamingOutputConnectionStream.WriteAsync` writes a variable-length chunk-size
prefix through `connection.Output.WriteAsync`, writes payload using another
`WriteAsync`, then explicitly calls `FlushAsync`. A pipeline writer's
`WriteAsync` already flushes; it is not just a buffered copy.

The synchronous `Write` blocks on `WriteAsync(...).GetAwaiter().GetResult()`.
`Flush` similarly blocks. `BeginWrite`/`EndWrite` adapt the async write to APM.
This matters when an encoder's nominally asynchronous serialization path falls
back to synchronous XML writer methods.

## Design Notes

Do not equate every flush with a native pipe flush or a scheduling suspension:
pipeline flushes can complete synchronously. In issue #1779 diagnostics the
large payload writes suspended on backpressure, while header writes and the
additional explicit flushes did not.

The transport decides actual I/O granularity. For named pipes,
`NamedPipeConnectionContext` writes each pipeline segment separately.
Framing-only header batching did not resolve that investigation's slowdown.

Timeouts are derived from `_timeouts.SendTimeout` using `TimeoutHelper`.
Any optimization must preserve framing boundaries, cancellation/timeouts,
bounded buffering, and shutdown behavior across all framing transports.
