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

using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Databricks;
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

public class CalculatedMeasurementsQueryFixture : IAsyncLifetime
{
    public CalculatedMeasurementsQueryFixture()
    {
        var integrationTestConfiguration = new IntegrationTestConfiguration();

        DatabricksSchemaManager = new DatabricksSchemaManager(
            new DataHub.Core.FunctionApp.TestCommon.Databricks.HttpClientFactory(),
            integrationTestConfiguration.DatabricksSettings,
            schemaPrefix: nameof(CalculatedMeasurementsQueryFixture).ToLower());

        QueryOptions = new DatabricksQueryOptions
        {
            DatabaseName = DatabricksSchemaManager.SchemaName,
            CatalogName = "hive_metastore",
        };
    }

    public DatabricksSchemaManager DatabricksSchemaManager { get; }

    public DatabricksQueryOptions QueryOptions { get; }

    public async Task InitializeAsync()
    {
        await DatabricksSchemaManager.CreateSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        await DatabricksSchemaManager.DropSchemaAsync();
    }
}
