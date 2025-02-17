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

using DbUp;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Database;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.WholesaleMigration.Wholesale;
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
                .WithScript("20250217150000_CalculationsTable", GetScriptForCalculations())
                .LogToConsole()
                .Build();

        var result = upgrader.PerformUpgrade();
        return !result.Successful
            ? throw new Exception("Wholesale database migration failed", result.Error) : true;
    }

    private string GetScriptForCalculations()
    {
        return """
        CREATE TABLE [dbo].[Calculation](
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

        ALTER TABLE [dbo].[Calculation] ADD  DEFAULT ((0)) FOR [CalculationType]
        GO

        ALTER TABLE [dbo].[Calculation] ADD  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [CreatedByUserId]
        GO

        ALTER TABLE [dbo].[Calculation] ADD  DEFAULT (getdate()) FOR [CreatedTime]
        GO

        ALTER TABLE [dbo].[Calculation] ADD  DEFAULT ((0)) FOR [IsInternalCalculation]
        GO
        """;
    }
}
