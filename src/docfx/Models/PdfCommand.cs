// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using Microsoft.DocAsCode.Pdf;
using Spectre.Console.Cli;

#nullable enable

namespace Microsoft.DocAsCode;

class PdfCommand : AsyncCommand<PdfCommand.Settings>
{
    [Description("Creates PDF files for each TOC file in a directory")]
    public class Settings : CommandSettings
    {
        [Description("Path to the directory containing toc.json files")]
        [CommandArgument(0, "[directory]")]
        public string? Directory { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await PdfBuilder.CreatePdfForDirectory(settings.Directory ?? Directory.GetCurrentDirectory(), new());
        return 0;
    }
}
