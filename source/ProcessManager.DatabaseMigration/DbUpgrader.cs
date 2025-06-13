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

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DbUp;
using DbUp.Engine;
using Energinet.DataHub.ProcessManager.DatabaseMigration.Extensibility.DbUp;
using Microsoft.Data.SqlClient;

namespace Energinet.DataHub.ProcessManager.DatabaseMigration;

public static class DbUpgrader
{
    public static DatabaseUpgradeResult DatabaseUpgrade(
        string connectionString,
        string environment = "")
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        // We create the schema in code to ensure we can create the 'SchemaVersions'
        // table within the schema
        var schemaName = "pm";
        CreateSchema(connectionString, schemaName);

        var upgradeEngine = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptNameComparer(new ScriptComparer())
            .WithScripts(new CustomScriptProvider(Assembly.GetExecutingAssembly(), GetScriptFilter(environment)))
            .LogToConsole()
            .WithExecutionTimeout(TimeSpan.FromHours(1))
            .JournalToSqlTable(schemaName, "SchemaVersions")
            .Build();

        var result = upgradeEngine.PerformUpgrade();
        return result;
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "SQL doesn't contain user input")]
    private static void CreateSchema(string connectionString, string schemaName)
    {
        var createProcessManagerSchemaSql = $@"
            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{schemaName}')
            BEGIN
                EXEC('CREATE SCHEMA {schemaName}');
            END";

        // Execute the pre-deployment script to create the schema if it doesn't exist.
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = new SqlCommand(createProcessManagerSchemaSql, connection);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// We do not have a common implementation for handling DB migrations.
    /// But we do use the same technique for executing a script based on environment
    /// in both EDI and Process Manager. So if we make changes to this code, we should
    /// probaly update it in both repositories.
    /// </summary>
    private static Func<string, bool> GetScriptFilter(string environment)
    {
        if (environment.Contains("DEV") || environment.Contains("TEST"))
        {
            // In DEV and TEST environments we want to apply an additional script
            return file =>
                file.Contains("202506131200 Grant access to query execution plan.sql", StringComparison.OrdinalIgnoreCase)
                || IsModelScriptFile(file);
        }

        // In other environments we only want to apply "Model" script files
        return file => IsModelScriptFile(file);
    }

    /// <summary>
    /// Based on the filename this method determines if the script is a "Model"
    /// script file, which is a script file used to create the database schema/model.
    /// </summary>
    private static bool IsModelScriptFile(string file)
    {
        return
            file.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            && file.Contains(".Scripts.", StringComparison.OrdinalIgnoreCase)
            && !file.Contains(".Permissions.", StringComparison.OrdinalIgnoreCase);
    }
}
