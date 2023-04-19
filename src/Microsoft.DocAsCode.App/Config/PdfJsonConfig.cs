// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DocAsCode.HtmlToPdf;
using Newtonsoft.Json;

namespace Microsoft.DocAsCode;

[Serializable]
internal class PdfJsonConfig : BuildJsonConfig
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("host")]
    public new string Host { get; set; }

    [JsonProperty("locale")]
    public string Locale { get; set; }

    [JsonProperty("generatesExternalLink")]
    public bool GeneratesExternalLink { get; set; }

    [JsonProperty("excludeDefaultToc")]
    public bool ExcludeDefaultToc { get; set; }

    [JsonProperty("excludedTocs")]
    public List<string> ExcludedTocs { get; set; }

    [JsonProperty("base")]
    public string BasePath { get; set; }

    /// <summary>
    /// Specify options specific to the wkhtmltopdf tooling used by the pdf command.
    /// </summary>
    [JsonProperty("wkhtmltopdf")]
    public WkhtmltopdfJsonConfig Wkhtmltopdf { get; set; }

    /// <summary>
    /// Gets or sets the "Table of Contents" bookmark title.
    /// </summary>
    [JsonProperty("tocTitle")]
    public string TocTitle { get; set; } = "Table of Contents";

    /// <summary>
    /// Gets or sets the outline option.
    /// </summary>
    [JsonProperty("outline")]
    public OutlineOption OutlineOption { get; set; } = OutlineOption.DefaultOutline;

    /// <summary>
    /// Gets or sets the cover page title.
    /// </summary>
    [JsonProperty("coverTitle")]
    public string CoverPageTitle { get; set; } = "Cover Page";
}
