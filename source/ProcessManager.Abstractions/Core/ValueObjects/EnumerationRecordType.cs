// Copyright 2020 Energinet DataHub A/S
//
// Licensed under the Apache License, Version 2.0 (the "License2");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Reflection;

namespace Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;

/// <summary>
/// Base for enumeration types which are records with
/// a static fields per unique name in the enumeration.
/// </summary>
public abstract record EnumerationRecordType
{
    protected EnumerationRecordType(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>
    /// Returns all public static fields declared on the type <typeparamref name="TEnumerationRecordType"/>.
    /// </summary>
    public static IEnumerable<TEnumerationRecordType> GetAll<TEnumerationRecordType>()
        where TEnumerationRecordType : EnumerationRecordType
    {
        var fields = typeof(TEnumerationRecordType)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        return fields
            .Select(f => f.GetValue(null))
            .Cast<TEnumerationRecordType>();
    }
}
