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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample.V1;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1.Activities;

public class PerformBusinessValidationActivity_Brs_X03_V1(
    BusinessValidator<ActorRequestProcessExampleInputV1> validator)
{
    private readonly BusinessValidator<ActorRequestProcessExampleInputV1> _validator = validator;

    [Function(nameof(PerformBusinessValidationActivity_Brs_X03_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput activityInput)
    {
        var validationErrors = await _validator.ValidateAsync(activityInput.Input).ConfigureAwait(false);

        return new ActivityOutput(
                validationErrors);
    }

    public record ActivityInput(
        ActorRequestProcessExampleInputV1 Input);

    public record ActivityOutput(
        IReadOnlyCollection<ValidationError> ValidationErrors);
}
