using System.Text.Json.Serialization;

#nullable enable

namespace Microsoft.DocAsCode.Pdf;

/// <summary>
/// A PDF outline shares the same data shape as toc.json output
/// </summary>
class PdfOutline
{
    public bool EnablePdf { get; set; }

    public string? Name { get; set; }

    public PdfOutline[]? Items { get; set; }

    public string? Href { get; set; }

    /// <summary>
    /// The PDF /obj ID for this outline node
    /// </summary>
    [JsonIgnore]
    internal int PdfId;

    /// <summary>
    /// Number of decadents outline nodes
    /// </summary>
    [JsonIgnore]
    internal int Count;
}

record struct PdfUrl(Uri? PageUrl, string? ExternalUrl);
