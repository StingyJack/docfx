// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;
using Spectre.Console.Cli;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DocAsCode;

internal class PdfCommand : Command<PdfCommandOptions>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] PdfCommandOptions options)
    {
        return CommandHelper.Run(options, () =>
        {
            var Config = ParseOptions(options, out var BaseDirectory, out var OutputFolder);
            RunPdf.Exec(Config, new(), BaseDirectory, OutputFolder);
        });
    }

    private static PdfJsonConfig ParseOptions(PdfCommandOptions options, out string baseDirectory, out string outputFolder)
    {
        (var config, baseDirectory) = CommandHelper.GetConfig<PdfConfig>(options.ConfigFile);
        outputFolder = options.OutputFolder;
        MergeOptionsToConfig(options, config.Item, baseDirectory);
        return config.Item;
    }

    private static void MergeOptionsToConfig(PdfCommandOptions options, PdfJsonConfig config, string configDirectory)
    {
        BuildCommand.MergeOptionsToConfig(options, config, configDirectory);

        if (options.ExcludedTocs is not null && options.ExcludedTocs.Any())
        {
            config.ExcludedTocs = new ListWithStringFallback(options.ExcludedTocs);
        }

        if (!string.IsNullOrEmpty(options.Host))
        {
            config.Host = options.Host;
        }

        if (!string.IsNullOrEmpty(options.BasePath))
        {
            config.BasePath = options.BasePath;
        }

        if (options.GeneratesExternalLink.HasValue)
        {
            config.GeneratesExternalLink = options.GeneratesExternalLink.Value;
        }
    }

    private sealed class PdfConfig
    {
        [JsonProperty("pdf")]
        public PdfJsonConfig Item { get; set; }
    }
}
