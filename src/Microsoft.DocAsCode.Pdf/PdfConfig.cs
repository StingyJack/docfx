// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Playwright;

#nullable enable

namespace Microsoft.DocAsCode;

internal class PdfConfig
{
    /// <summary>
    /// Base URL for external links. If null, links to pages not included in the TOC are not clickable.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    public bool? DisplayHeaderFooter { get; set; }

    /// <summary>
    /// <para>
    /// HTML template for the print footer. Should use the same format as the <paramref
    /// name="headerTemplate"/>.
    /// </para>
    /// </summary>
    public string? FooterTemplate { get; set; }

    /// <summary>
    /// <para>
    /// Paper format. If set, takes priority over <paramref name="width"/> or <paramref
    /// name="height"/> options. Defaults to 'Letter'.
    /// </para>
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// <para>
    /// HTML template for the print header. Should be valid HTML markup with following classes
    /// used to inject printing values into them:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>'date'</c> formatted print date</description></item>
    /// <item><description><c>'title'</c> document title</description></item>
    /// <item><description><c>'url'</c> document location</description></item>
    /// <item><description><c>'pageNumber'</c> current page number</description></item>
    /// <item><description><c>'totalPages'</c> total pages in the document</description></item>
    /// </list>
    /// </summary>
    public string? HeaderTemplate { get; set; }

    public string? Height { get; set; }

    public bool? Landscape { get; set; }

    public Margin? Margin { get; set; }

    /// <summary><para>Print background graphics. Defaults to <c>false</c>.</para></summary>
    public bool? PrintBackground { get; set; }
}
