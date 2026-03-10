# Message — Abstract WCF message with dispose-on-close semantics

**Source:** `src/CoreWCF.Primitives/src/CoreWCF/Channels/Message.cs`
**Namespace:** `CoreWCF.Channels`
**Last validated:** 2026-03-10

## Purpose

Abstract base class for all WCF messages. Manages message state (Created, Read, Written,
Copied, Closed) and provides access to headers, properties, and body content.

## Key API

- **`Close()`** — Sets `State` to `MessageState.Closed`, calls `OnClose()`. After this,
  `IsDisposed` returns true.
- **`Properties`** — Abstract. Returns `MessageProperties` dictionary.
- **`Headers`** — Abstract. Returns `MessageHeaders`.
- **`State`** / **`IsDisposed`** — `IsDisposed` is true when `State == MessageState.Closed`.

## Concrete Message Types and IsDisposed Checks

All concrete types in this file check `IsDisposed` in their `Properties` and `Headers` getters,
throwing `ObjectDisposedException` (via `CreateMessageDisposedException()`) when the message
is closed:

| Type | Line | Checks IsDisposed in Properties? |
|------|------|----------------------------------|
| `BodyWriterMessage` | ~1092 | ✅ Yes |
| `StreamedMessage` (ReceivedMessage) | ~1399 | ❌ No (returns `_properties` directly) |
| `BufferedMessage` | ~1634 | ✅ Yes |

**BufferedMessage** is the most common type for HTTP buffered requests (created by
`TextMessageEncoder.ReadMessage`). Accessing `Properties` after `Close()` throws
`ObjectDisposedException("", SR.MessageClosed)`.

## Design Notes

- `CreateMessageDisposedException()` returns `new ObjectDisposedException("", SR.MessageClosed)`
- The `IsDisposed` property is simply `State == MessageState.Closed`
- `Close()` is idempotent — calling it when already closed is a no-op
- Message state transitions are one-way: once closed, a message cannot be reopened
