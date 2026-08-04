// Bind the page fill colour to the "layer 2" neutral, the same binding the .NET Aspire dashboard
// applies. Without it every Fluent surface sits one layer too light and the explorer reads as a
// generic Fluent app rather than part of the dashboard.
// Relative import so the page keeps working under a non-root path base.
import { fillColor, neutralLayerL2 }
    from "../_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.lib.module.js";

fillColor.setValueFor(document.body, neutralLayerL2);
