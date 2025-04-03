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

namespace Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;

/// <summary>
/// Extends enumeration types with strongly typed methods using <typeparamref name="TDataHubRecordType"/>.
/// </summary>
public abstract record DataHubRecordType<TDataHubRecordType> : EnumerationRecordType
    where TDataHubRecordType : DataHubRecordType<TDataHubRecordType>
{
    protected DataHubRecordType(string name)
        : base(name)
    {
    }

    /// <summary>
    /// Get instance of enumeration type by name.
    /// </summary>
    /// <param name="name">Name that identifies enumeration type.</param>
    /// <returns>Instance of enumeration type.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="name"/> doesn't match a defined enumeration type.</exception>
    public static TDataHubRecordType FromName(string name)
    {
        return FromNameOrDefault(name)
               ?? throw new InvalidOperationException($"{name} is not a valid {typeof(TDataHubRecordType).Name} {nameof(name)}");
    }

    /// <summary>
    /// Get instance of enumeration type by name, or null.
    /// </summary>
    /// <param name="name">Name that identifies enumeration type.</param>
    /// <returns><see langword="null"/> if <paramref name="name"/> is <see langword="null"/>;
    /// otherwise instance of enumeration type.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="name"/> is not <see langword="null"/> and
    /// doesn't match a defined enumeration type.</exception>
    public static TDataHubRecordType? FromNameOrDefault(string? name)
    {
        return name is null
            ? null
            : GetAll<TDataHubRecordType>().SingleOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
