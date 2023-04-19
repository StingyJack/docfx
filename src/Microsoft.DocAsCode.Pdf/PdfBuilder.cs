// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.DocAsCode.Common;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

#nullable enable

namespace Microsoft.DocAsCode.Pdf;

static class PdfBuilder
{
    public static async Task CreatePdfForDirectory(string directory, PdfConfig config)
    {
        directory = Path.GetFullPath(directory);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var tocs = (
            from path in Directory.EnumerateFiles(directory, "toc.json", new EnumerationOptions { RecurseSubdirectories = true })
            let toc = JsonSerializer.Deserialize<PdfOutline>(File.ReadAllBytes(path), jsonOptions)
            let url = new Uri(Path.GetRelativePath(directory, path), UriKind.Relative)
            where toc != null && toc.EnablePdf
            select (path, url, toc)).ToList();

        if (tocs.Count <= 0)
        {
            Logger.LogWarning($"No toc.json is not available with {{ \"enablePdf\": true }} in {directory}");
            return;
        }

        using var server = Serve(directory);
        await server.StartAsync();
        var serverUrl = new Uri(server.Urls.First());

        using var http = new HttpClient();
        using var playwright = await Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var browserPagePool = new ConcurrentBag<IPage>();

        var pdfUrls = tocs.SelectMany(item => GetPrintableUrls(item.url, item.toc)).ToHashSet();
        var pdfBytes = new ConcurrentDictionary<Uri, byte[]?>();

        await Parallel.ForEachAsync(pdfUrls, async (pdfUrl, _) =>
        {
            pdfBytes[pdfUrl] = await PrintPdfFile(pdfUrl);
        });

        await Parallel.ForEachAsync(tocs, async (item, _) =>
        {
            using var output = File.Create(Path.ChangeExtension(item.path, ".pdf"));

            await PdfMerger.MergePdf(
                PipeWriter.Create(output),
                item.toc,
                href => ParsePdfUrl(item.url, href),
                url => pdfBytes[url] is { } bytes ? PipeReader.Create(new(bytes)) : null);
        });

        IEnumerable<Uri> GetPrintableUrls(Uri tocRelativeUrl, PdfOutline node)
        {
            if (ParsePdfUrl(tocRelativeUrl, node.Href).PageUrl is { } url)
                yield return url;

            if (node.Items is not null)
                foreach (var item in node.Items)
                    foreach (var uri in GetPrintableUrls(tocRelativeUrl, item))
                        yield return uri;
        }

        PdfUrl ParsePdfUrl(Uri tocRelativeUrl, string? href)
        {
            if (href is null)
                return default;

            var url = new Uri(href, UriKind.RelativeOrAbsolute);
            var externalUrl = config.BaseUrl is null ? "" : new Uri(new(config.BaseUrl, tocRelativeUrl), href).ToString();
            if (url.IsAbsoluteUri)
                return new(null, externalUrl);

            var pageUrl = new Uri(new(serverUrl, tocRelativeUrl), href);
            return new(pageUrl, externalUrl);
        }

        async Task<byte[]?> PrintPdfFile(Uri url)
        {
            var page = browserPagePool.TryTake(out var existingPage) ? existingPage : await browser.NewPageAsync();

            try
            {
                var pageResponse = await page.GotoAsync(url.ToString());
                if (pageResponse is null || !pageResponse.Ok)
                {
                    Logger.LogWarning($"Cannot print PDF: [{pageResponse?.Status}] {url}");
                    return null;
                }

                return await page.PdfAsync(new()
                {
                    DisplayHeaderFooter = config.DisplayHeaderFooter,
                    HeaderTemplate = config.HeaderTemplate,
                    FooterTemplate = config.FooterTemplate,
                    Margin = config.Margin,
                    Landscape = config.Landscape,
                    Format = config.Format,
                    PrintBackground = config.PrintBackground,
                });
            }
            finally
            {
                browserPagePool.Add(page);
            }
        }
    }

    private static WebApplication Serve(string directory)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(directory),
        });
        return app;
    }
}
