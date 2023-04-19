// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Reflection;
using System.Text;

#nullable enable

namespace Microsoft.DocAsCode.Pdf;

static class PdfMerger
{
    private const int MinStructParentNum = 100000;

    private readonly static string? s_toolVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
    private readonly static byte[] s_info = Encoding.ASCII.GetBytes($"/Creator (docfx {s_toolVersion})\n");
    private readonly static byte[] s_pdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x25, 0xD3, 0xEB, 0xE9, 0xE1, 0x0A };

    /// <summary>
    /// Merges multiple PDF files to a single PDF file.
    /// </summary>
    /// <param name="parseUrl">
    /// Given an href defined in PdfOutline, returns an Uri if this href should be included in the PDF, or null if the href is an external URL.
    /// </param>
    /// <remarks>
    /// This method works directly on PDF's bytes and guts,
    /// it does not attempt to support all PDF types,
    /// it only works with PDFs produced from a specific version of Chrome.
    ///
    /// References:
    /// <list type="bullet">
    ///   <item>Basic introduction of PDF file format: https://resources.infosecinstitute.com/topic/pdf-file-format-basic-structure/</item>
    ///   <item>PDF 1.4 Specification: https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/pdfreference1.4.pdf</item>
    ///   <item>Chrome Print PDF Source Code: https://source.chromium.org/chromium/chromium/src/+/main:third_party/skia/src/pdf/</item>
    /// </list> 
    /// </remarks>
    public static async Task MergePdf(PipeWriter writer, PdfOutline outline, Func<string?, PdfUrl> parseUrl, Func<Uri, PipeReader?> readPdf)
    {
        var position = 0;
        var baseId = 0;
        var baseStructParentsNum = 0;
        var baseStructParentNum = 0;
        var xrefs = new Dictionary<int, long>();
        var structElems = new List<int>();
        var structParents = new List<int>();
        var structParent = new List<int>();
        var pages = new List<int>();
        var urlDests = new Dictionary<Uri, int>();
        var urlIds = new Dictionary<Uri, int>();
        InitUrlIds(outline);

        Write(s_pdfHeader);
        await WritePdf(outline);
        WriteTrailer();
        await writer.CompleteAsync();

        void InitUrlIds(PdfOutline outline)
        {
            if (parseUrl(outline.Href).PageUrl is { } url && !urlIds.ContainsKey(url))
                urlIds.Add(url, urlIds.Count);

            if (outline.Items != null)
                foreach (var child in outline.Items)
                    InitUrlIds(child);
        }

        async Task WritePdf(PdfOutline outline)
        {
            if (parseUrl(outline.Href).PageUrl is { } url && readPdf(url) is { } reader)
            {
                await WriteOnePdf(url, reader);
                await writer.FlushAsync();
            }

            if (outline.Items != null)
                foreach (var child in outline.Items)
                    await WritePdf(child);
        }

        async ValueTask WriteOnePdf(Uri url, PipeReader reader)
        {
            var objectCount = 0;
            var structParentCount = structParent.Count;
            var pagesCount = pages.Count;

            while (true)
            {
                var result = await reader.ReadAsync();
                var buffer = result.Buffer;
                TryProcessPdfObject(ref buffer);
                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted)
                    break;
            }

            await reader.CompleteAsync();

            baseId += objectCount;
            baseStructParentNum += structParent.Count - structParentCount;

            // Blank pages does not have entries in /ParentTree /Nums.
            while (structParents.Count < pages.Count)
                structParents.Add(0);
            baseStructParentsNum = structParents.Count;

            if (pages.Count > pagesCount)
                urlDests.Add(url, pages[pagesCount]);

            void TryProcessPdfObject(ref ReadOnlySequence<byte> buffer)
            {
                if (buffer.IsSingleSegment)
                {
                    var span = buffer.FirstSpan;
                    while (true)
                    {
                        var idEnd = span.IndexOf(" 0 obj\n"u8);
                        if (idEnd < 0 || !TryParseIntFromEnd(span, idEnd, out var id, out _))
                            return;

                        var endobj = span.IndexOf("endobj\n"u8);
                        if (idEnd < 0 || endobj <= idEnd)
                            return;

                        ProcessPdfObject(id, span[(idEnd + " 0 obj\n"u8.Length)..endobj]);

                        var consumed = endobj + "endobj\n"u8.Length;
                        span = span[consumed..];
                        buffer = buffer.Slice(consumed);
                    }
                }
                else
                {
                    TryProcessPdfObjectMultiSegments(ref buffer);
                }
            }

            void TryProcessPdfObjectMultiSegments(ref ReadOnlySequence<byte> buffer)
            {
                var sequenceReader = new SequenceReader<byte>(buffer);
                while (!sequenceReader.End)
                {
                    if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> idSpan, " 0 obj\n"u8) ||
                        !TryParseIntFromEnd(idSpan, idSpan.Length, out var id, out _))
                        return;

                    if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> span, "endobj\n"u8))
                        return;

                    ProcessPdfObject(id, span);

                    buffer = buffer.Slice(sequenceReader.Position);
                }
            }

            void ProcessPdfObject(int id, ReadOnlySpan<byte> bytes)
            {
                xrefs.Add(baseId + id, position);

                // Update and write object id
                WriteInt(baseId + id);
                Write(" 0 obj\n"u8);
                CopyWriteObject(baseId, id, bytes);
                Write("endobj\n"u8);

                objectCount++;
            }
        }

        void CopyWriteObject(int baseId, int id, ReadOnlySpan<byte> span)
        {
            // Skip the first object which is alway /Creator /Producer /CreationDate /ModDate
            if (id == 1)
                return;

            var isPage = false;
            var isDocumentStructElem = false;
            var structParentIndex = -1;

            if (span.StartsWith("<</"u8))
            {
                if (span[3..].StartsWith("Limits "u8))
                    return;

                if (span[3..].StartsWith("Type /"u8))
                {
                    var typeSpan = span[9..];
                    if (typeSpan.StartsWith("Catalog"u8) || typeSpan.StartsWith("StructTreeRoot"u8))
                        return;

                    if (typeSpan.StartsWith("ParentTree\n"u8))
                    {
                        AddNums(baseId, span);
                        return;
                    }

                    if ((structParentIndex = span.IndexOf("\n/StructParent"u8)) >= 0)
                        structParentIndex += "\n/StructParent"u8.Length;

                    if (isPage = typeSpan.StartsWith("Page\n"u8))
                        pages.Add(baseId + id);

                    if (isDocumentStructElem = typeSpan.StartsWith("StructElem\n/S /Document\n"u8))
                        structElems.Add(baseId + id);
                }
            }

            while (true)
            {
                // Write Object References in form of "{id} 0 R"
                var idEnd = span.IndexOf(" 0 R"u8);
                if (idEnd >= 0 && TryParseIntFromEnd(span, idEnd, out var refId, out var chunkStart) &&
                    (structParentIndex < 0 || idEnd < structParentIndex))
                {
                    Write(span[..chunkStart]);

                    if (isPage && span[..chunkStart].EndsWith("/Parent "u8))
                    {
                        // Update page parent to 1000002
                        Write("1000002 0 R"u8);
                    }
                    else if (isDocumentStructElem && span[..chunkStart].EndsWith("/P "u8))
                    {
                        // Update document struct element parent to 1000003
                        Write("1000003 0 R"u8);
                    }
                    else
                    {
                        // Update and write object id reference
                        WriteInt(baseId + refId);
                        Write(" 0 R"u8);
                    }

                    var chunkEnd = idEnd + " 0 R"u8.Length;
                    if (structParentIndex >= 0)
                        structParentIndex -= chunkEnd;
                    span = span[chunkEnd..];
                    continue;
                }

                if (structParentIndex < 0)
                    break;

                // Write "/StructParents {num}" or "/StructParent {num}"
                if (span[structParentIndex] == (byte)'s')
                    structParentIndex++;

                structParentIndex++;
                Write(span[..structParentIndex]);

                var parseSucceed = Utf8Parser.TryParse(span[structParentIndex..], out int num, out var bytesConsumed);
                Debug.Assert(parseSucceed);

                // /StructParents are always < 100000
                if (num < MinStructParentNum)
                    WriteInt(baseStructParentsNum + num);
                else
                    WriteInt(baseStructParentNum + num);

                span = span[(structParentIndex + bytesConsumed)..];
                structParentIndex = -1;
            }

            Write(span);
        }

        void AddNums(int baseId, ReadOnlySpan<byte> span)
        {
            var nums = span[(span.IndexOf((byte)'[') + 1)..];

            while (true)
            {
                if (!Utf8Parser.TryParse(nums, out int n, out var bytesConsumed))
                    break;
                nums = nums[(bytesConsumed + 1)..];
                if (!Utf8Parser.TryParse(nums, out int id, out bytesConsumed))
                    break;
                nums = nums[(bytesConsumed + 5)..];

                // /StructParents are always < 100000
                if (n < MinStructParentNum)
                    structParents.Add(baseId + id);
                else
                    structParent.Add(baseId + id);
            }
        }

        void WriteTrailer()
        {
            var outlineId = xrefs.Count + 1;
            var mutableOutlineId = outlineId;
            UpdateOutline(outline, ref mutableOutlineId);
            WriteOutline(outline);

            // Write info
            var infoPosition = position;
            Write("1000000 0 obj\n<<"u8);
            Write(s_info);
            Write(">>\nendobj\n"u8);

            // Write catalog
            var catalogPosition = position;
            Write("1000001 0 obj\n<</Type /Catalog\n/Pages 1000002 0 R\n/Dests 1000005 0 R\n/PageMode /UseOutlines\n/Outlines "u8);
            WriteInt(outlineId);
            Write(" 0 R\n/MarkInfo <</Type /MarkInfo\n/Marked true>>\n/StructTreeRoot 1000003 0 R>>\nendobj\n"u8);

            // Write Pages
            var pagesPosition = position;
            Write("1000002 0 obj\n<</Type /Pages\n/Count "u8);
            WriteInt(pages.Count);
            Write("\n/Kids ["u8);
            foreach (var page in pages)
            {
                WriteInt(page);
                Write(" 0 R "u8);
            }
            Write("]>>\nendobj\n"u8);

            var structTreeRootPosition = position;
            Write("1000003 0 obj\n<</Type /StructTreeRoot\n/K ["u8);
            foreach (var id in structElems)
            {
                WriteInt(id);
                Write(" 0 R "u8);
            }
            Write("] \n/ParentTree 1000004 0 R>>\nendobj\n"u8);

            var parentTreePosition = position;
            Write("1000004 0 obj\n<</Type /ParentTree\n/Nums ["u8);
            for (var i = 0; i < structParents.Count; i++)
            {
                if (structParents[i] > 0)
                {
                    WriteInt(i);
                    Write(" "u8);
                    WriteInt(structParents[i]);
                    Write(" 0 R "u8);
                }
            }
            for (var i = 0; i < structParent.Count; i++)
            {
                WriteInt(MinStructParentNum + i);
                Write(" "u8);
                WriteInt(structParent[i]);
                Write(" 0 R "u8);
            }
            Write("]>>\nendobj\n"u8);

            var destsPosition = position;
            Write("1000005 0 obj\n<<"u8);
            foreach (var (key, value) in urlDests)
            {
                Write("\n/URLD-"u8);
                WriteInt(urlIds[key]);
                Write(" ["u8);
                WriteInt(value);
                Write(" 0 R /Fit]"u8);
            }
            Write(">>\nendobj\n"u8);

            // Write xrefs
            var startxref = position;
            Write("xref\n0 "u8);
            WriteInt(xrefs.Count + 1);
            Write("\n0000000000 65535 f \n"u8);
            for (var i = 1; i <= xrefs.Count; i++)
            {
                WriteLongD(xrefs[i], 10);
                Write(" 00000 n \n"u8);
            }

            Write("1000000 5\n"u8);
            WriteLongD(infoPosition, 10);
            Write(" 00000 n \n"u8);
            WriteLongD(catalogPosition, 10);
            Write(" 00000 n \n"u8);
            WriteLongD(pagesPosition, 10);
            Write(" 00000 n \n"u8);
            WriteLongD(structTreeRootPosition, 10);
            Write(" 00000 n \n"u8);
            WriteLongD(parentTreePosition, 10);
            Write(" 00000 n \n"u8);
            WriteLongD(destsPosition, 10);
            Write(" 00000 n \n"u8);

            // Write trailer
            Write("trailer\n<</Size "u8);
            WriteInt(xrefs.Count + 7);
            Write("\n/Root 1000001 0 R\n/Info 1000000 0 R>>\nstartxref\n"u8);
            WriteInt(startxref);
            Write("\n%%EOF"u8);
        }

        void UpdateOutline(PdfOutline node, ref int id)
        {
            node.PdfId = id++;

            if (node.Items is { } items)
            {
                foreach (var item in items)
                {
                    UpdateOutline(item, ref id);
                    node.Count += item.Count;
                }
            }
        }

        void WriteOutline(PdfOutline node, PdfOutline? next = null)
        {
            xrefs.Add(node.PdfId, position);
            WriteInt(node.PdfId);
            Write(" 0 obj\n<</Type /Outlines\n/Count "u8);
            WriteInt(node.Count);

            var items = node.Items;
            if (items != null && items.Length > 0)
            {
                Write("\n/First "u8);
                WriteInt(items[0].PdfId);
                Write(" 0 R\n/Last "u8);
                WriteInt(items[^1].PdfId);
            }
            if (next != null)
            {
                Write("\n/Next "u8);
                WriteInt(next.PdfId);
                Write(" 0 R"u8);
            }

            Write("\n/Title "u8);
            WriteHexString(node.Name ?? "");

            var pdfUrl = parseUrl(node.Href);
            if (pdfUrl.PageUrl is { } url && urlIds.TryGetValue(url, out var urlId))
            {
                Write("\n/Dest /URLD-"u8);
                WriteInt(urlId);
            }
            else if (pdfUrl.ExternalUrl is { } externalUrl)
            {
                Write("\n/A <</Type /Action\n/S /URI\n/URI ("u8);
                WriteASCIIString(externalUrl);
                Write(")>>\n"u8);
            }
            Write(">>\nendobj\n"u8);

            if (items != null)
            {
                for (var i = 0; i < items.Length - 1; i++)
                    WriteOutline(items[i], items[i + 1]);
                WriteOutline(items[^1], null);
            }
        }

        void Write(ReadOnlySpan<byte> bytes)
        {
            var span = writer.GetSpan(bytes.Length);
            bytes.CopyTo(span);
            writer.Advance(bytes.Length);
            position += bytes.Length;
        }

        void WriteInt(int value)
        {
            var span = writer.GetSpan(20);
            Utf8Formatter.TryFormat(value, span, out var bytesWritten);
            writer.Advance(bytesWritten);
            position += bytesWritten;
        }

        void WriteLongD(long value, byte length)
        {
            var span = writer.GetSpan(20);
            Utf8Formatter.TryFormat(value, span, out var bytesWritten, new('D', length));
            writer.Advance(bytesWritten);
            position += bytesWritten;
        }

        void WriteHexString(string value)
        {
            var bytes = Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value));
            var span = writer.GetSpan(bytes.Length + 6);
            "<FEFF"u8.CopyTo(span);
            Encoding.ASCII.GetBytes(bytes, span[5..]);
            span[bytes.Length + 5] = (byte)'>';
            writer.Advance(bytes.Length + 6);
            position += bytes.Length + 6;
        }

        void WriteASCIIString(string value)
        {
            var byteLength = Encoding.ASCII.GetByteCount(value);
            var span = writer.GetSpan(byteLength);
            Encoding.ASCII.GetBytes(value, span);
            writer.Advance(byteLength);
            position += byteLength;
        }
    }

    static bool TryParseIntFromEnd(ReadOnlySpan<byte> span, int end, out int id, out int start)
    {
        start = end;
        while (start > 0 && char.IsNumber((char)span[start - 1]))
            start--;

        return Utf8Parser.TryParse(span[start..end], out id, out var _);
    }
}
