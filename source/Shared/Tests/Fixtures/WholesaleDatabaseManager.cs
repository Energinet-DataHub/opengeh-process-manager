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

using System.Text;
using DbUp;
using DbUp.Engine;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;

public class WholesaleDatabaseManager(string name)
    : SqlServerDatabaseManager<WholesaleContext>(name + $"_{DateTime.Now:yyyyMMddHHmm}_")
{
    /// <inheritdoc/>
    public override WholesaleContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<WholesaleContext>()
            .UseSqlServer(ConnectionString, options =>
            {
                options.UseNodaTime();
                options.EnableRetryOnFailure();
            });

        return (WholesaleContext)Activator.CreateInstance(typeof(WholesaleContext), optionsBuilder.Options)!;
    }

    /// <summary>
    /// Creates the database schema using DbUp instead of a database context.
    /// </summary>
    protected override Task<bool> CreateDatabaseSchemaAsync(WholesaleContext context)
    {
        return Task.FromResult(CreateDatabaseSchema(context));
    }

    /// <summary>
    /// Creates the database schema using DbUp instead of a database context.
    /// </summary>
    protected override bool CreateDatabaseSchema(WholesaleContext context)
    {
        var upgrader =
            DeployChanges.To
                .SqlDatabase(ConnectionString)
                .WithScripts(new[]
                {
                    new SqlScript("20250217140000_CalculationsSchema", GetScriptForCalculationsSchema()),
                    new SqlScript("20250217150000_CalculationsTable", GetScriptForCalculationsTable()),
                    new SqlScript("20250217160000_SeedCalculationsTable", GetScriptForSeedingCalculationsTable()),
                })
                .LogToConsole()
                .Build();

        var result = upgrader.PerformUpgrade();
        return !result.Successful
            ? throw new Exception("Wholesale database migration failed", result.Error) : true;
    }

    private static string GetScriptForCalculationsSchema()
    {
        return """
        CREATE SCHEMA [calculations]
        GO
        """;
    }

    private static string GetScriptForCalculationsTable()
    {
        return """
        CREATE TABLE [calculations].[Calculation](
            [Id] [uniqueidentifier] NOT NULL,
            [GridAreaCodes] [varchar](max) NOT NULL,
            [ExecutionState] [int] NOT NULL,
            [CalculationJobId] [bigint] NULL,
            [PeriodStart] [datetime2](7) NOT NULL,
            [PeriodEnd] [datetime2](7) NOT NULL,
            [ExecutionTimeStart] [datetime2](7) NULL,
            [ExecutionTimeEnd] [datetime2](7) NULL,
            [AreSettlementReportsCreated] [bit] NOT NULL,
            [CalculationType] [int] NOT NULL,
            [CreatedByUserId] [uniqueidentifier] NOT NULL,
            [CreatedTime] [datetime2](7) NOT NULL,
            [Version] [bigint] NOT NULL,
            [OrchestrationState] [int] NOT NULL,
            [ActorMessagesEnqueuedTimeEnd] [datetime2](7) NULL,
            [ActorMessagesEnqueuingTimeStart] [datetime2](7) NULL,
            [CompletedTime] [datetime2](7) NULL,
            [ScheduledAt] [datetime2](7) NOT NULL,
            [OrchestrationInstanceId] [nvarchar](256) NOT NULL,
            [CanceledByUserId] [uniqueidentifier] NULL,
            [IsInternalCalculation] [bit] NOT NULL,
         CONSTRAINT [PK_Calculation] PRIMARY KEY NONCLUSTERED
        (
            [Id] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
        GO

        ALTER TABLE [calculations].[Calculation] ADD  DEFAULT ((0)) FOR [CalculationType]
        GO

        ALTER TABLE [calculations].[Calculation] ADD  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [CreatedByUserId]
        GO

        ALTER TABLE [calculations].[Calculation] ADD  DEFAULT (getdate()) FOR [CreatedTime]
        GO

        ALTER TABLE [calculations].[Calculation] ADD  DEFAULT ((0)) FOR [IsInternalCalculation]
        GO
        """;
    }

    private static string GetScriptForSeedingCalculationsTable()
    {
        var csvData = GetCsvToInsertIntoCalculationsTable();
        var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var columnNames = lines[0].Split(',');

        var script = new StringBuilder();

        script.Append("INSERT INTO [calculations].[Calculation] (");
        script.Append(string.Join(", ", columnNames));
        script.Append(") VALUES");

        for (var rowIndex = 1; rowIndex < lines.Length; rowIndex++)
        {
            script.Append('(');

            // Column values must be split on ';' because some values contains ','
            var columnValues = lines[rowIndex].Split(';');

            script.Append(ConvertToUniqueIdentifierOrNull(columnValues[0]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[1]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[2]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[3]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[4]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[5]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[6]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[7]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[8]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[9]));
            script.Append(", ");
            script.Append(ConvertToUniqueIdentifierOrNull(columnValues[10]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[11]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[12]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[13]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[14]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[15]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[16]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[17]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[18]));
            script.Append(", ");
            script.Append(ConvertToUniqueIdentifierOrNull(columnValues[19]));
            script.Append(", ");
            script.Append(ConvertToStringOrNull(columnValues[20]));

            script.Append(')');
            if (rowIndex != lines.Length - 1)
                script.Append(", ");
        }

        var result = script.ToString();
        return result;
    }

    private static string ConvertToUniqueIdentifierOrNull(string columnValue)
    {
        return string.IsNullOrWhiteSpace(columnValue)
            ? "null" :
            $"CONVERT(uniqueidentifier, '{columnValue}')";
    }

    private static string ConvertToStringOrNull(string columnValue)
    {
        return string.IsNullOrWhiteSpace(columnValue)
            ? "null" :
            $"'{columnValue}'";
    }

    /// <summary>
    /// Column values must be separated by ';' because some values contain ','.
    /// </summary>
    private static string GetCsvToInsertIntoCalculationsTable()
    {
        return """
        Id,GridAreaCodes,ExecutionState,CalculationJobId,PeriodStart,PeriodEnd,ExecutionTimeStart,ExecutionTimeEnd,AreSettlementReportsCreated,CalculationType,CreatedByUserId,CreatedTime,Version,OrchestrationState,ActorMessagesEnqueuedTimeEnd,ActorMessagesEnqueuingTimeStart,CompletedTime,ScheduledAt,OrchestrationInstanceId,CanceledByUserId,IsInternalCalculation
        53730493-3654-4270-a67f-44b394d5cd3c;["533","543","584","950"];2;701426971102580;2023-03-31T22:00:00.0000000;2023-04-30T22:00:00.0000000;2024-09-03T12:42:51.9918851;2024-09-03T13:11:43.0506350;False;3;2ab9810b-ac63-4ead-7532-08dbfd4f5820;2024-09-03T12:42:42.8552495;638609641628552766;8;2024-09-03T13:13:43.0792946;2024-09-03T13:12:27.0067188;2024-09-03T13:13:43.1987413;2024-09-03T12:42:42.7838207;3575a39749854e0dbf311fdd4474ea2b;;False
        d49e67f3-b17e-4a6b-bfb3-73ffa9077985;["533","543","584","803","804","950"];2;205003943232686;2022-12-31T23:00:00.0000000;2023-01-31T23:00:00.0000000;2024-09-05T09:58:01.8594757;2024-09-05T10:08:45.2382991;False;1;5bfaa00b-cfbe-440f-15ef-08dc65f606a6;2024-09-05T09:57:58.6951104;638611270786960323;8;;;2024-09-05T10:08:45.3138692;2024-09-05T09:57:57.9992065;b20e306a66c6423594b59bb91b1f321a;;True
        3391709e-952e-44c0-aa30-1d0fb660e6dc;["533","543","584","803","804","950"];2;7687438413974;2023-02-28T23:00:00.0000000;2023-03-31T22:00:00.0000000;2024-09-05T10:13:10.8317115;2024-09-05T10:24:04.8852063;False;1;5bfaa00b-cfbe-440f-15ef-08dc65f606a6;2024-09-05T10:13:09.0811670;638611279890812104;8;;;2024-09-05T10:24:04.9472262;2024-09-05T10:13:09.0142538;dc1f0c314e7c495f8d64c16f5c62d6eb;;True
        """;
    }
}
