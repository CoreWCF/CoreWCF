# HttpRequestContext — WCF request context wrapping ASP.NET Core HttpContext

**Source:** `src/CoreWCF.Http/src/CoreWCF/Channels/HttpRequestContext.cs`
**Namespace:** `CoreWCF.Channels`
**Last validated:** 2026-03-10

## Purpose

Bridges ASP.NET Core's `HttpContext` with CoreWCF's `RequestContextBase`. Manages authentication,
message lifecycle (set/close), and reply sending for a single HTTP request.

## Key API

- **`SetMessage(Message message, Exception requestException)`** — Stores either the message or
  exception on the base class. When `requestException` is non-null, it calls
  `SetRequestMessage(requestException)` to store the error, then **closes the original message
  via `message.Close()`**. When exception is null, it sets the security property and stores the
  message via `SetRequestMessage(message)`.

- **`ProcessAuthenticationAsync()`** — Validates authentication; returns false to reject.

- **`GetHttpInput(bool isRequest)`** — Creates an `HttpInput` for parsing the request body.

- **`SendResponseAndCloseAsync(HttpStatusCode, string)`** — Sends a raw HTTP response.

## Internal Structure

### SetMessage behavior (critical for understanding the disposed-message bug):

```
if (requestException != null)
    SetRequestMessage(requestException)  // stores exception in base class
    message.Close()                      // DISPOSES the original message
else
    message.Properties.Security = ...
    SetRequestMessage(message)           // stores message in base class
```

After `SetMessage` with a non-null exception, the original message is **closed/disposed**.
Any code that accesses the message after this point will get `ObjectDisposedException`.

The base class `RequestContextBase.RequestMessage` getter throws the stored exception when
`_requestMessageException` is non-null, so the dispatcher gets the error when it tries to
read the request.

## Dependencies

- Extends `RequestContextBase` (in `src/Common/src/CoreWCF/Channels/RequestContextBase.cs`)
- Uses `HttpInput` for message parsing
- Uses `HttpOutput` for response sending

## Relationships

- Created by `AspNetCoreReplyChannel.HandleRequest` via `HttpRequestContext.CreateContext()`
- Passed to `ChannelDispatcher.DispatchAsync()` for service operation routing
- `RequestContextBase.RequestMessage` property throws stored exception or returns stored message
