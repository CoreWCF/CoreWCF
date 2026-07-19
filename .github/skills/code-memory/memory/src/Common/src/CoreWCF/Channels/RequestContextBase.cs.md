# RequestContextBase — Base class for WCF request contexts

**Source:** `src/Common/src/CoreWCF/Channels/RequestContextBase.cs`
**Namespace:** `CoreWCF.Channels`
**Last validated:** 2026-03-10

## Purpose

Abstract base class for WCF request contexts. Stores either a request message or an exception
(never both). When the dispatcher accesses `RequestMessage`, it either returns the stored
message or throws the stored exception.

## Key API

- **`RequestMessage`** (property getter) — If `_requestMessageException` is non-null, throws it.
  Otherwise returns `_requestMessage`. This is how addressing errors propagate to the dispatcher.

- **`SetRequestMessage(Message)`** — Stores a valid message. Asserts no exception is stored.
- **`SetRequestMessage(Exception)`** — Stores an error exception. Asserts no message is stored.
- **`ReplySent`** — Task that completes when the reply has been sent.

## Design Notes

- `SetRequestMessage(Exception)` and `SetRequestMessage(Message)` have `Fx.Assert` guards
  ensuring mutual exclusivity — you cannot have both a message and an exception stored.
- The `RequestMessage` getter pattern (throw-on-access) is how protocol errors from
  `ProcessHttpAddressing` propagate through the dispatch pipeline without special error plumbing.
- Subclasses: `HttpRequestContext`, `QueueMessageContext`
