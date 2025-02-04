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

namespace Energinet.DataHub.ProcessManager.Components.ValueObjects;

/// <summary>
/// Base for enumeration types which are records with
/// a static fields per unique name in the enumeration.
/// </summary>
public abstract record SlimEnumerationType
{
    protected SlimEnumerationType(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>
    /// Returns all public static fields declared on the type <typeparamref name="TEnumerationType"/>.
    /// </summary>
    public static IEnumerable<TEnumerationType> GetAll<TEnumerationType>()
        where TEnumerationType : SlimEnumerationType
    {
        var fields = typeof(TEnumerationType)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        return fields
            .Select(f => f.GetValue(null))
            .Cast<TEnumerationType>();
    }
}
