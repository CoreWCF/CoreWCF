# AspNetCoreReplyChannel — HTTP request handler for CoreWCF's ASP.NET Core transport

**Source:** `src/CoreWCF.Http/src/CoreWCF/Channels/AspNetCoreReplyChannel.cs`
**Namespace:** `CoreWCF.Channels`
**Last validated:** 2026-03-10

## Purpose

Handles incoming HTTP requests for CoreWCF services hosted on ASP.NET Core. The key method
`HandleRequest(HttpContext)` orchestrates authentication, message parsing, property injection,
dispatching, and error handling for each inbound WCF request.

## Key API

- **`HandleRequest(HttpContext context)`** — Main entry point called by middleware. Parses the
  HTTP request into a WCF `Message`, sets it on the request context, injects the `HttpContext`
  as a message property, then dispatches via `ChannelDispatcher.DispatchAsync`.

## Internal Structure — HandleRequest Flow

1. Creates `HttpRequestContext` via `HttpRequestContext.CreateContext(_httpSettings, context)`
2. Runs `ProcessAuthenticationAsync()` — returns early on failure
3. Gets `HttpInput` from the request context
4. Calls `httpInput.ParseIncomingMessageAsync()` → returns `(Message, Exception)` tuple
   - Both null → sends 400 BadRequest and returns
   - Exception comes from `ProcessHttpAddressing` (e.g., `ActionMismatchAddressingException`)
5. Adds `"Microsoft.AspNetCore.Http.HttpContext"` property to the message
6. Calls `requestContext.SetMessage(message, exception)` — **if exception is non-null, this
   closes/disposes the original message** (see HttpRequestContext below)
7. Dispatches via `ChannelDispatcher.DispatchAsync(requestContext)`
8. Awaits `requestContext.ReplySent`

**Bug fixed (2026-03-10):** Steps 5 and 6 were originally reversed. When both message and
exception were non-null, `SetMessage` disposed the message, then step 5 threw
`ObjectDisposedException` accessing `Message.Properties`. Fix: moved Properties.Add before
SetMessage.

### Error handling

`HandleProcessInboundException(ex, requestContext)` catches all exceptions from the try block:
- `ProtocolException` → sends HTTP status from exception data (default 400)
- Other exceptions → sends 400 BadRequest

## Dependencies

- **`HttpRequestContext`** — wraps the ASP.NET Core `HttpContext` for WCF processing
- **`HttpInput`** — parses HTTP request body into a WCF `Message`
- **`ChannelDispatcher`** — dispatches the parsed request to the service operation
- **`_httpSettings`** — configuration for HTTP transport behavior

## Relationships

- Called by `ServiceModelHttpMiddleware` which routes ASP.NET Core requests to WCF endpoints
- `HttpInput.ParseIncomingMessageAsync()` → `ProcessHttpAddressing()` can return errors
- `HttpRequestContext.SetMessage()` stores message or exception on the `RequestContextBase`
- `ChannelDispatcher.DispatchAsync()` routes to the appropriate endpoint/operation
