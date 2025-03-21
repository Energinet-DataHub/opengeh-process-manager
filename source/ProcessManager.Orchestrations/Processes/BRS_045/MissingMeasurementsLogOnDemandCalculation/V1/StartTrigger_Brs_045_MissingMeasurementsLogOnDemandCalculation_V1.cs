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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1;

internal class StartTrigger_Brs_045_MissingMeasurementsLogOnDemandCalculation_V1(
    StartCalculationHandlerV1 handler)
{
    /// <summary>
    /// Start a BRS-045_MissingMeasurementsLogOnDemandCalculation calculation and return its id.
    /// </summary>
    [Function(nameof(StartTrigger_Brs_045_MissingMeasurementsLogOnDemandCalculation_V1))]
    [Authorize]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orchestrationinstance/command/start/custom/brs_045_missingmeasurementslogondemandcalculation/1")]
        HttpRequest httpRequest,
        [FromBody]
        StartCalculationCommandV1 command,
        FunctionContext executionContext)
    {
        var orchestrationInstanceId = await handler.HandleAsync(command).ConfigureAwait(false);
        return new OkObjectResult(orchestrationInstanceId);
    }
}
