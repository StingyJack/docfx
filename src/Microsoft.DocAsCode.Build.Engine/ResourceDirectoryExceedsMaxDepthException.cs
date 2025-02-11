// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DocAsCode.Exceptions;

namespace Microsoft.DocAsCode.Build.Engine;

public class ResourceFileExceedsMaxDepthException : DocfxException
{
    public int MaxDepth { get; }
    public string DirectoryName { get; }
    public string ResourceName { get; }

    public ResourceFileExceedsMaxDepthException(int maxDepth, string fileName, string resourceName) : base($"Resource file \"{fileName}\" in resource \"{resourceName}\" exceeds the max allowed depth {maxDepth}.")
    {
        MaxDepth = maxDepth;
        DirectoryName = fileName;
        ResourceName = resourceName;
    }
}
