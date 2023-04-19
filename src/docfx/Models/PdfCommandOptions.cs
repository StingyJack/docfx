// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Spectre.Console.Cli;

namespace Microsoft.DocAsCode;

[Description("Generate pdf file")]
internal class PdfCommandOptions : BuildCommandOptions
{
    [Description("Specify whether or not to generate external links for PDF")]
    [CommandOption("--generatesExternalLink")]
    public bool? GeneratesExternalLink { get; set; }

    [Description("Specify the hostname to link not-in-TOC articles")]
    [CommandOption("--host")]
    public new string Host { get; set; }

    [Description("Specify the toc files to be excluded")]
    [CommandOption("--excludedTocs")]
    public IEnumerable<string> ExcludedTocs { get; set; }

    [Description("Specify the base path to generate external link, {host}/{locale}/{basePath}")]
    [CommandOption("--basePath")]
    public string BasePath { get; set; }
}
