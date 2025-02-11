﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.DocAsCode.Build.Common;
using Microsoft.DocAsCode.DataContracts.UniversalReference;
using Microsoft.DocAsCode.Plugins;

namespace Microsoft.DocAsCode.Build.UniversalReference;

[Export(nameof(UniversalReferenceDocumentProcessor), typeof(IDocumentBuildStep))]
public class ApplyOverwriteDocumentForUref : ApplyOverwriteDocument
{
    public override string Name => nameof(ApplyOverwriteDocumentForUref);

    public override int BuildOrder => 0x10;

    public IEnumerable<ItemViewModel> GetItemsFromOverwriteDocument(FileModel fileModel, string uid, IHostService host)
    {
        return Transform<ItemViewModel>(
            fileModel,
            uid,
            host);
    }

    public IEnumerable<ItemViewModel> GetItemsToOverwrite(FileModel fileModel, string uid, IHostService host)
    {
        return ((PageViewModel)fileModel.Content).Items.Where(s => s.Uid == uid);
    }

    protected override void ApplyOverwrite(IHostService host, List<FileModel> overwrites, string uid, List<FileModel> articles)
    {
        ApplyOverwrite(host, overwrites, uid, articles, GetItemsFromOverwriteDocument, GetItemsToOverwrite);
    }
}
