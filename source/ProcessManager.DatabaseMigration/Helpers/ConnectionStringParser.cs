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

namespace Energinet.DataHub.ProcessManager.DatabaseMigration.Helpers;

internal static class ConnectionStringParser
{
    /// <summary>
    /// A default connection string.
    /// </summary>
    /// <remarks>
    /// If you are migrating to SQL Server Express use connection string format "Server=(LocalDb)\\MSSQLLocalDB;..."
    /// If you are migrating to SQL Server use connection string format "Server=localhost;..."
    /// </remarks>
    private const string DefaultConnectionString = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Database=ProcessManager";

    public static string Parse(string[] args)
    {
        return args.FirstOrDefault() ?? DefaultConnectionString;
    }
}
