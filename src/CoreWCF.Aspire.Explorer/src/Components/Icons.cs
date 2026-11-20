// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Aspire.Explorer.Components;

/// <summary>
/// Inline SVG glyphs, rendered with <c>@((MarkupString)Icons.Xyz)</c>.
/// <para>
/// Hand-written rather than referenced from <c>Microsoft.FluentUI.AspNetCore.Components.Icons</c>,
/// which would add roughly 40 MB of assembly to the published <c>corewcf/aspire-explorer</c> container
/// image for the handful of glyphs this UI needs. They inherit <c>currentColor</c>, so they follow the
/// theme with no extra styling.
/// </para>
/// </summary>
public static class Icons
{
    private const string StrokeHead =
        """<svg viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" focusable="false" aria-hidden="true">""";

    private const string FillHead =
        """<svg viewBox="0 0 20 20" width="16" height="16" fill="currentColor" focusable="false" aria-hidden="true">""";

    /// <summary>Circular arrow, for "reload the WSDL".</summary>
    public const string Reload = StrokeHead + """<path d="M15.6 6.4A6.5 6.5 0 1 0 16.5 10" /><path d="M16.5 3.4v3.5H13" />""" + "</svg>";

    /// <summary>Overlapping sheets, for "copy to clipboard".</summary>
    public const string Copy = StrokeHead + """<rect x="7" y="7" width="8.5" height="8.5" rx="1.5" /><path d="M12.8 4.5H6a1.5 1.5 0 0 0-1.5 1.5v6.8" />""" + "</svg>";

    /// <summary>Check mark, shown briefly after a successful copy.</summary>
    public const string Check = StrokeHead + """<path d="m4.5 10.5 3.5 3.5 7.5-8" />""" + "</svg>";

    /// <summary>Filled triangle, for "invoke".</summary>
    public const string Play = FillHead + """<path d="M6.6 4.4c0-.7.7-1.1 1.3-.8l7 5.1a1 1 0 0 1 0 1.6l-7 5.1a1 1 0 0 1-1.3-.8Z" />""" + "</svg>";

    /// <summary>Filled square, for "cancel the in-flight invocation".</summary>
    public const string Stop = FillHead + """<rect x="5.5" y="5.5" width="9" height="9" rx="1.6" />""" + "</svg>";

    /// <summary>Isometric box, marking a service in the tree.</summary>
    public const string Service = StrokeHead + """<path d="M10 2.8 16.8 6.4v7.2L10 17.2 3.2 13.6V6.4Z" /><path d="m3.2 6.4 6.8 3.6 6.8-3.6M10 10v7.2" />""" + "</svg>";

    /// <summary>Document with a folded corner, marking a contract in the tree.</summary>
    public const string Contract = StrokeHead + """<path d="M5.8 3.2h5.4L15 7v9a1 1 0 0 1-1 1H5.8a1 1 0 0 1-1-1V4.2a1 1 0 0 1 1-1Z" /><path d="M11.2 3.2V7H15" />""" + "</svg>";

    /// <summary>Rightward arrow, marking an operation (a call) in the tree.</summary>
    public const string Operation = StrokeHead + """<path d="M4.2 10h10.4" /><path d="m11.4 6.8 3.2 3.2-3.2 3.2" />""" + "</svg>";

    /// <summary>Larger box, used by the "nothing selected yet" placeholder.</summary>
    public const string ServiceLarge =
        """<svg viewBox="0 0 20 20" width="48" height="48" fill="none" stroke="currentColor" stroke-width="1" stroke-linecap="round" stroke-linejoin="round" focusable="false" aria-hidden="true"><path d="M10 2.8 16.8 6.4v7.2L10 17.2 3.2 13.6V6.4Z" /><path d="m3.2 6.4 6.8 3.6 6.8-3.6M10 10v7.2" /></svg>""";
}
