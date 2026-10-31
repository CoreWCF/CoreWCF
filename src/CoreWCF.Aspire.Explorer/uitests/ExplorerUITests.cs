// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace CoreWCF.Aspire.Explorer.UITests;

/// <summary>
/// Feature-level tests for the explorer UI, driven through a real browser against the real app.
/// Several of these pin behaviour that regressed during development and would be invisible to a
/// unit test - row geometry, keyboard operation, and when a bound value actually reaches the server.
/// </summary>
[Collection(nameof(ExplorerCollection))]
public sealed class ExplorerUITests(ExplorerFixture fixture)
{
    private readonly ExplorerFixture _fixture = fixture;

    /// <summary>Left edge of every operation label, used to detect horizontal drift in the tree.</summary>
    private static Task<double[]> OperationLabelOffsetsAsync(IPage page)
        => page.EvalOnSelectorAllAsync<double[]>(
            "[data-operation-id] .row-text",
            "els => els.map(e => Math.round(e.getBoundingClientRect().left))");

    private const string Soap11Service = "Calculator service";

    /// <summary>
    /// Clicks an operation and waits for the detail pane to catch up. Selection round-trips over the
    /// Blazor circuit, so asserting on the pane before the title has changed races the render.
    /// <para>
    /// Targeted by the tree's own identity rather than by accessible name: the same contract is
    /// hosted over both SOAP versions, so an operation name alone matches two rows.
    /// </para>
    /// </summary>
    private static async Task SelectOperationAsync(
        IPage page, string operationName, string serviceName = Soap11Service)
    {
        var id = $"{serviceName}|ICalculatorService|{operationName}";
        await page.Locator($"[data-operation-id='{id}']").ClickAsync();
        await Assertions.Expect(page.Locator(".detail-title")).ToHaveTextAsync(operationName);
    }

    /// <summary>
    /// Types a parameter value and lets it reach the server.
    /// <para>
    /// The parameter cells bind on input with an 80 ms debounce, and nothing in the UI echoes the
    /// bound value back, so there is no state to poll - a settle is the only way to observe it. This
    /// is not a workaround for the blur bug: focus deliberately stays in the cell, so a binding that
    /// only committed on blur would still send the previous value however long the test waited.
    /// </para>
    /// </summary>
    private static async Task SetParameterAsync(IPage page, string parameterName, string value)
    {
        await page.GetByLabel($"Value of {parameterName}").FillAsync(value);
        await page.WaitForTimeoutAsync(300);
    }

    [Fact]
    public async Task Tree_lists_every_service_and_operation_without_being_expanded()
    {
        var page = await _fixture.NewPageAsync();

        // Services load up front. A lazily loaded node has no children, so fluent-tree-item draws no
        // chevron and there would be nothing to click to trigger the load.
        // Four calculator operations over each SOAP version, plus two inventory ones.
        await Assertions.Expect(page.Locator("[data-operation-id]")).ToHaveCountAsync(10);

        foreach (var operation in new[] { "Add", "Describe", "Fail", "PlaceOrder", "IsInStock", "GetQuantity" })
        {
            await Assertions.Expect(page.GetByRole(AriaRole.Treeitem, new() { Name = $"Operation {operation}" }).First)
                .ToBeVisibleAsync();
        }

        await Assertions.Expect(page.Locator(".toolbar-count")).ToHaveTextAsync("10 operations in 3 services");
    }

    [Fact]
    public async Task Stylesheets_are_served_whatever_the_environment()
    {
        var page = await _fixture.NewPageAsync();

        // The fixture runs the explorer in the default environment, not Development. Razor class
        // library content and the scoped-CSS bundle are static web assets, which WebApplicationBuilder
        // only wires up in Development - so without the app's own UseStaticWebAssets call these 404
        // and every Fluent component renders unstyled.
        var bundleLoaded = await page.EvaluateAsync<bool>(
            """
            () => Array.from(document.styleSheets).some(s =>
                (s.href || '').includes('CoreWCF.Aspire.Explorer.styles.css') && s.cssRules.length > 0)
            """);
        Assert.True(bundleLoaded, "The scoped-CSS bundle did not load.");

        var rebootLoaded = await page.EvaluateAsync<bool>(
            """
            () => Array.from(document.styleSheets).some(s =>
                (s.href || '').includes('Microsoft.FluentUI.AspNetCore.Components') && s.cssRules.length > 0)
            """);
        Assert.True(rebootLoaded, "The Fluent UI stylesheet did not load.");

        // Belt and braces: an unstyled header would be transparent rather than painted with the
        // chrome surface colour.
        var headerBackground = await page.Locator(".app-header")
            .EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        Assert.NotEqual("rgba(0, 0, 0, 0)", headerBackground);
    }

    [Fact]
    public async Task Selecting_an_operation_shows_its_endpoint_and_parameters()
    {
        var page = await _fixture.NewPageAsync();

        await SelectOperationAsync(page, "Add");

        await Assertions.Expect(page.Locator(".detail-contract")).ToContainTextAsync("ICalculatorService");
        await Assertions.Expect(page.Locator(".meta-code").First).ToContainTextAsync("/calc");
        await Assertions.Expect(page.GetByLabel("Value of x")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Value of y")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Selecting_an_operation_does_not_shift_the_row_sideways()
    {
        var page = await _fixture.NewPageAsync();

        var before = await OperationLabelOffsetsAsync(page);
        await SelectOperationAsync(page, "Add");
        var after = await OperationLabelOffsetsAsync(page);

        // Regression guard. Selection has to travel on an attribute Blazor owns outright, never on
        // Class: a Class that changes between renders makes Blazor rewrite the whole class attribute,
        // which destroys the "nested" class fluent-tree-item adds itself to indent child rows. The
        // selected row then jumps 16px to the left.
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Operations_can_be_selected_from_the_keyboard()
    {
        var page = await _fixture.NewPageAsync();

        // Start from a known row rather than counting Tab stops into the tree - the toolbar's stops
        // vary with whether the filter box is showing its clear button.
        await SelectOperationAsync(page, "Add");

        // From here on it is keyboard only: walk down the tree and open what is focused.
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator(".detail-title")).ToHaveTextAsync("Describe");
    }

    [Fact]
    public async Task Ctrl_Enter_invokes_with_the_value_still_being_typed()
    {
        var page = await _fixture.NewPageAsync();

        await SelectOperationAsync(page, "Add");
        await SetParameterAsync(page, "x", "17");
        await SetParameterAsync(page, "y", "25");

        // No blur between typing and invoking - the shortcut fires while the cell still has focus.
        // With change-only binding the second value never reaches the server and this returns 17.
        await page.Keyboard.PressAsync("Control+Enter");

        await Assertions.Expect(page.Locator(".response-status")).ToContainTextAsync("200 OK");

        // .First: the class lands on the grid and on elements it renders inside itself.
        await Assertions.Expect(page.Locator(".resp-grid").First).ToContainTextAsync("AddResult");
        await Assertions.Expect(page.Locator(".resp-grid").First).ToContainTextAsync("42");
    }

    [Fact]
    public async Task Filter_narrows_the_tree_and_the_count()
    {
        var page = await _fixture.NewPageAsync();

        await page.GetByRole(AriaRole.Searchbox).FillAsync("stock");

        await Assertions.Expect(page.Locator("[data-operation-id]")).ToHaveCountAsync(1);
        await Assertions.Expect(page.GetByRole(AriaRole.Treeitem, new() { Name = "Operation IsInStock" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".toolbar-count")).ToHaveTextAsync("1 operation in 1 service");

        await page.GetByRole(AriaRole.Searchbox).FillAsync("");
        await Assertions.Expect(page.Locator("[data-operation-id]")).ToHaveCountAsync(10);
    }

    [Fact]
    public async Task Complex_parameters_fall_back_to_the_xml_editor()
    {
        var page = await _fixture.NewPageAsync();

        await SelectOperationAsync(page, "PlaceOrder");

        // A data contract cannot be expressed by the Name/Type/Value grid, so the formatted tab
        // disables itself and the pre-filled envelope is the way in.
        await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Formatted" }).First)
            .ToBeDisabledAsync();

        // Read the property off the custom element itself: fluent-text-area is not an <input> or a
        // <textarea>, so InputValueAsync rejects it, and the envelope is a value rather than text.
        var envelope = await page.Locator(".xml-editor").First.EvaluateAsync<string>("el => el.value");
        Assert.Contains("PlaceOrder", envelope);
        Assert.Contains("Quantity", envelope);
        Assert.Contains("Sku", envelope);
    }

    [Fact]
    public async Task Operations_can_be_invoked_over_soap_12()
    {
        var page = await _fixture.NewPageAsync();

        await SelectOperationAsync(page, "Add", ExplorerFixture.Soap12ServiceName);

        // The version is read from the WSDL binding, not assumed.
        await Assertions.Expect(page.Locator(".meta")).ToContainTextAsync("1.2");
        await Assertions.Expect(page.Locator(".meta")).ToContainTextAsync("/calc12");

        await SetParameterAsync(page, "x", "17");
        await SetParameterAsync(page, "y", "25");
        await page.Keyboard.PressAsync("Control+Enter");

        await Assertions.Expect(page.Locator(".response-status")).ToContainTextAsync("200 OK");
        await Assertions.Expect(page.Locator(".resp-grid").First).ToContainTextAsync("42");

        // The reply really is a 1.2 envelope, not a 1.1 one that happened to work.
        await page.GetByRole(AriaRole.Tab, new() { Name = "XML" }).Last.ClickAsync();
        await Assertions.Expect(page.Locator(".response-xml"))
            .ToContainTextAsync("http://www.w3.org/2003/05/soap-envelope");
    }

    [Fact]
    public async Task Soap_12_faults_report_the_code_as_1_2_spells_it()
    {
        var page = await _fixture.NewPageAsync();

        await SelectOperationAsync(page, "Fail", ExplorerFixture.Soap12ServiceName);
        await SetParameterAsync(page, "reason", "disk full");
        await page.Keyboard.PressAsync("Control+Enter");

        // Same failure, different spelling: 1.1 says Client, 1.2 says Sender. Reading the fault with
        // MessageFault rather than by hunting for element names is what makes this come out right.
        await Assertions.Expect(page.Locator(".response-view")).ToContainTextAsync("SOAP fault (Sender)");
        await Assertions.Expect(page.Locator(".response-view")).ToContainTextAsync("disk full");
    }

    [Fact]
    public async Task Soap_faults_are_surfaced_rather_than_shown_as_a_bare_500()
    {
        var page = await _fixture.NewPageAsync();

        await SelectOperationAsync(page, "Fail");
        await SetParameterAsync(page, "reason", "disk full");
        await page.Keyboard.PressAsync("Control+Enter");

        await Assertions.Expect(page.Locator(".response-status")).ToContainTextAsync("500");

        // Assert on the captured text rather than with Expect, so a mismatch reports what the pane
        // actually said instead of just timing out.
        var response = await page.Locator(".response-view").TextContentAsync() ?? string.Empty;
        Assert.Contains("disk full", response);

        // 1.1 spells the code Client; the 1.2 endpoint is covered separately.
        Assert.Contains("SOAP fault (Client)", response);
    }
}
