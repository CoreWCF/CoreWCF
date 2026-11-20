# Explorer UI tests

Browser-driven tests for the SOAP Explorer, covering the features described in
[`Documentation/AspireExplorer`](../../../Documentation/AspireExplorer/readme.md).

## Running them

```bash
dotnet test src/CoreWCF.Aspire.Explorer/uitests/CoreWCF.Aspire.Explorer.UITests.csproj -f net8.0
```

The first run downloads a Chromium build (roughly 150 MB) into the shared Playwright browser cache.
Later runs reuse it. Nothing else has to be installed or started by hand: the fixture hosts the
CoreWCF endpoints in-process, launches the real explorer as a child process on a free port, and
tears both down afterwards.

## Why they are not in the CI matrix

`.github/actions/run-tests` sweeps every project matching `CoreWCF.*.Tests.csproj`. This project is
called `CoreWCF.Aspire.Explorer.UITests`, which deliberately does **not** match that glob — those CI
legs provide neither a browser nor a running app.

The name still ends in `Tests`, which is what `Directory.Build.props` keys on to supply xunit and
the test SDK. Both halves matter: renaming the project to `...Explorer.Tests` would silently enrol
it in every CI leg, and renaming it away from `Tests` would strip its test infrastructure.

## What they cover

| Test | Feature |
| --- | --- |
| `Stylesheets_are_served_whatever_the_environment` | Static web assets load outside Development |
| `Tree_lists_every_service_and_operation_without_being_expanded` | Services are read at start-up, so the tree arrives populated |
| `Selecting_an_operation_shows_its_endpoint_and_parameters` | Detail pane: metadata row and the parameter grid |
| `Selecting_an_operation_does_not_shift_the_row_sideways` | Tree geometry is stable across selection |
| `Operations_can_be_selected_from_the_keyboard` | Arrow keys walk the tree, <kbd>Enter</kbd> opens the focused row |
| `Ctrl_Enter_invokes_with_the_value_still_being_typed` | Values commit as typed, not on blur |
| `Filter_narrows_the_tree_and_the_count` | Toolbar filter and the match count |
| `Complex_parameters_fall_back_to_the_xml_editor` | Formatted tab disables itself for data contracts |
| `Soap_faults_are_surfaced_rather_than_shown_as_a_bare_500` | Fault string lifted out of the envelope |

Three of these guard behaviour that a unit test cannot see, and that regressed during development:
row geometry, keyboard operation, and the moment a bound value actually reaches the server.

## Notes for writing more

- The explorer is Blazor Server. Never wait on network idle — the SignalR socket never goes idle.
- **Prerendered DOM is dead DOM.** Blazor Server renders the whole tree server-side, so the rows are
  in the page long before a click can do anything, and a click that lands early is silently dropped.
  Waiting for the markup is therefore not enough. `NewPageAsync` waits for the `_blazor` WebSocket
  and then for a `_bl_*` attribute to appear on a row — Blazor stamps that on an element at the
  moment it wires up the element's handlers, so it is a precise "this row is live" signal.
- Parameter cells bind on input with a short debounce and nothing echoes the bound value back, so
  `SetParameterAsync` settles briefly after typing. That is about the debounce, not about focus:
  focus stays in the cell, so a binding that only committed on blur would still fail the test.
- Each test gets a fresh page. A shared page would leak the selection and the filter between tests.
- The explorer child process deliberately runs in the **default** environment, not Development, and
  with `--no-launch-profile`. `Stylesheets_are_served_whatever_the_environment` depends on that:
  `WebApplicationBuilder` only wires up static web assets in Development, so the app calls
  `UseStaticWebAssets()` itself. Forcing Development here would hide a regression in that call.
