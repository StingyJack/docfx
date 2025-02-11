﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using YamlDotNet.Serialization;

namespace Microsoft.DocAsCode.YamlSerialization.TypeInspectors;

public sealed class ExtensibleReadableAndWritablePropertiesTypeInspector : ExtensibleTypeInspectorSkeleton
{
    private readonly IExtensibleTypeInspector _innerTypeDescriptor;

    public ExtensibleReadableAndWritablePropertiesTypeInspector(IExtensibleTypeInspector innerTypeDescriptor)
    {
        _innerTypeDescriptor = innerTypeDescriptor;
    }

    public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container) =>
        from p in _innerTypeDescriptor.GetProperties(type, container)
        where p.CanWrite
        select p;

    public override IPropertyDescriptor GetProperty(Type type, object container, string name) =>
        _innerTypeDescriptor.GetProperty(type, container, name);
}
