# HttpInput / HttpChannelHelpers — HTTP message parsing and addressing validation

**Source:** `src/CoreWCF.Http/src/CoreWCF/Channels/HttpChannelHelpers.cs`
**Namespace:** `CoreWCF.Channels`
**Last validated:** 2026-03-10

## Purpose

`HttpInput` is an abstract class that reads an HTTP request body and decodes it into a WCF
`Message`. It also validates HTTP-level addressing (SOAPAction header vs SOAP message Action
header) via `ProcessHttpAddressing`.

## Key API

- **`ParseIncomingMessageAsync()`** — Returns `(Message message, Exception requestException)`.
  Reads the HTTP body, decodes it with the message encoder, then calls
  `ProcessHttpAddressing(message)` which may return an error exception. Both message and
  exception can be non-null simultaneously.

## Internal Structure

### ParseIncomingMessageAsync flow

1. `CheckForContentAsync()` — validates content presence
2. `ValidateContentType()` — checks Content-Type header
3. Reads message body via one of:
   - `ReadStreamedMessageAsync(stream)` — for streamed transfer
   - `ReadChunkedBufferedMessageAsync(stream)` — for chunked encoding
   - `ReadBufferedMessageAsync(stream)` — for known content-length
4. `ProcessHttpAddressing(message)` — returns exception or null
5. Returns `(message, exception)` tuple

### ProcessHttpAddressing — when it returns non-null exceptions

Three cases produce a non-null `result` exception:

1. **AddressingVersion.None + Action header present:**
   `ProtocolException("HttpAddressingNoneHeaderOnWire", "Action")`
   — Server expects no addressing but message has an Action header.

2. **AddressingVersion.None + To header present:**
   `ProtocolException("HttpAddressingNoneHeaderOnWire", "To")`

3. **HTTP SOAPAction ≠ SOAP message Action header:**
   `ActionMismatchAddressingException` — The SOAPAction HTTP header (for SOAP 1.1) or
   Content-Type action parameter (for SOAP 1.2) doesn't match `message.Headers.Action`.
   This is the case triggered when client and server use different bindings with conflicting
   action values.

### ProcessHttpAddressing — action extraction

- **SOAP 1.1:** Reads `SoapActionHeader` property (HTTP SOAPAction header), URL-decodes,
  strips surrounding quotes.
- **SOAP 1.2:** Reads from Content-Type `action` parameter or `start-info` action.
- **AddressingVersion.None:** Sets `message.Headers.Action` from the HTTP action.
- **Other addressing:** Compares HTTP action with `message.Headers.Action` (from SOAP headers).

### Key properties

- `SoapActionHeader` — abstract, returns the raw HTTP SOAPAction header value
- `ContentType` — the HTTP Content-Type header
- `ContentLength` — the HTTP Content-Length (-1 if unknown)
- `_messageEncoder` — the encoder used to decode the message body
- `_isRequest` — whether this is a request (vs response) message

## Design Notes

- The `ProcessHttpAddressing` method catches and swallows `XmlException` and
  `CommunicationException` when reading `message.Headers.Action` or `message.Headers.To`.
  This means if a header is malformed, the error is silently ignored.
- The method always calls `AddProperties(message)` first, which adds
  `HttpRequestMessageProperty` and sets `message.Properties.Via`.

## Relationships

- Created by `HttpRequestContext.GetHttpInput()`
- Called by `AspNetCoreReplyChannel.HandleRequest`
- Uses the configured `MessageEncoder` to decode messages
- `ActionMismatchAddressingException` has a `ProvideFault()` method used by the dispatcher
  to create SOAP fault responses
