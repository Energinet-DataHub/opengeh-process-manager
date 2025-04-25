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

using Energinet.DataHub.Core.Databricks.SqlStatementExecution;
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities.CalculationResultStep;

internal class CalculationResultStepGetCalculationResultsActivity_Brs_021_ElectricalHeatingCalculation_V1(
    [FromKeyedServices(DatabricksWorkspaceNames.Measurements)]
    CalculatedMeasurementsQuery calculatedMeasurementsQuery,
    DatabricksSqlWarehouseQueryExecutor databricksSqlWarehouseQueryExecutor)
{
    private readonly CalculatedMeasurementsQuery _calculatedMeasurementsQuery = calculatedMeasurementsQuery;
    private readonly DatabricksSqlWarehouseQueryExecutor _databricksSqlWarehouseQueryExecutor = databricksSqlWarehouseQueryExecutor;

    [Function(nameof(CalculationStepStartJobActivity_Brs_021_ElectricalHeatingCalculation_V1))]
    public async Task<List<QueryResult<CalculatedMeasurement>>> Run(
        [ActivityTrigger] ActivityInput input)
    {
        return await _calculatedMeasurementsQuery
            .GetAsync(_databricksSqlWarehouseQueryExecutor)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId);
}
