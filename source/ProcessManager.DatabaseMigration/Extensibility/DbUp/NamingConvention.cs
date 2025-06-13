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

using System.Text.RegularExpressions;

namespace Energinet.DataHub.ProcessManager.DatabaseMigration.Extensibility.DbUp;

/// <summary>
/// "Type" can be empty because we don't have scripts in a "Model" folder in Process Manager.
/// If we move them into a folder now, it will change their name as it is stored in the
/// "SchemaVersions" table, which would mean all scripts would be executed on an already
/// existing database, and that would fail.
/// </summary>
internal static class NamingConvention
{
    // Matches                                                   {type} {timestamp } {name}
    // Energinet.DataHub.ProcessManager.DatabaseMigration.Scripts.202103021434 First.sql
    // Energinet.DataHub.ProcessManager.DatabaseMigration.Scripts.Permission.202506061200 Second.sql
    public static readonly Regex Regex = new Regex(@".*Scripts\.(?<type>.*|Permissions)\.(?<timestamp>\d{12}) (?<name>).*\b.sql");
}
