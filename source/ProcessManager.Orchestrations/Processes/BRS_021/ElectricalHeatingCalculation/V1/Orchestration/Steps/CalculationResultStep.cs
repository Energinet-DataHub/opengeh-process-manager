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

using Energinet.DataHub.ProcessManager.Components.Databricks.Jobs.Model;
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities.CalculationResultStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities.CalculationStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements.Model;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Orchestration.Steps;

internal class CalculationResultStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceContext orchestrationInstanceContext)
    : StepExecutor(
        context,
        defaultRetryOptions,
        orchestrationInstanceContext.OrchestrationInstanceId)
{
    internal const string StepDescription = "Beregningsresult";
    internal const int CalculationResultStepSequence = 2;

    protected override int StepSequenceNumber => CalculationResultStepSequence;

    protected override async Task<StepInstanceTerminationState> OnExecuteAsync()
    {
        var todo = await Context.CallActivityAsync<List<QueryResult<CalculatedMeasurement>>>(
            nameof(CalculationResultStepGetCalculationResultsActivity_Brs_021_ElectricalHeatingCalculation_V1),
            new CalculationStepStartJobActivity_Brs_021_ElectricalHeatingCalculation_V1.ActivityInput(
                orchestrationInstanceContext.OrchestrationInstanceId),
            DefaultRetryOptions);

        return StepInstanceTerminationState.Succeeded;
    }
}
