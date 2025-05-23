﻿// Copyright 2020 Energinet DataHub A/S
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

using System.Globalization;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;

/// <summary>
/// This class is used to wrap the result (a dynamic type) of a Databricks SQL query row.
/// </summary>
public sealed record DatabricksSqlRow
{
    private readonly IDictionary<string, object?> _columns;

    public DatabricksSqlRow(IDictionary<string, object?> columns)
    {
        _columns = columns;
    }

    public string? this[string key]
    {
        get
        {
            var value = _columns[key];
            return value == null ? null : Convert.ToString(value);
        }
    }

    public bool HasColumn(string columnName)
    {
        return _columns.ContainsKey(columnName);
    }

    public override string ToString()
    {
        return _columns.Aggregate(string.Empty, (current, kvp) => current + $"Key = {kvp.Key}, Value = {kvp.Value}");
    }

    public string? ToNullableString(string columnName)
    {
        return _columns.TryGetValue(columnName, out var value) && value != null
            ? Convert.ToString(value)
            : null;
    }

    public string ToNonEmptyString(string columnName)
    {
        var value = ToNullableString(columnName);
        // We set 'paramName' to the 'columnName' so we get the name of the column which we failed on parsing.
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName: columnName);

        return value;
    }

    public Instant ToInstant(string columnName)
    {
        var value = ToNonEmptyString(columnName);

        return InstantPattern.ExtendedIso.Parse(value).Value;
    }

    public decimal ToDecimal(string columnName)
    {
        var value = ToNonEmptyString(columnName);

        return decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public decimal? ToNullableDecimal(string columnName)
    {
        var value = ToNullableString(columnName);

        return string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parse from Databricks "INT" to int.
    /// </summary>
    public int ToInt(string columnName)
    {
        var value = ToNonEmptyString(columnName);

        return int.Parse(value);
    }

    /// <summary>
    /// Parse from Databricks "BIGINT" to long.
    /// </summary>
    public long ToLong(string columnName)
    {
        var value = ToNonEmptyString(columnName);

        return long.Parse(value);
    }

    public Guid ToGuid(string columnName)
    {
        var value = ToNonEmptyString(columnName);

        return Guid.Parse(value);
    }

    public bool ToBool(string columnName)
    {
        var value = ToNonEmptyString(columnName);

        return value switch
        {
            "true" => true,
            "false" => false,

            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                actualValue: value,
                "Value does not contain a valid string representation of a boolean."),
        };
    }
}
