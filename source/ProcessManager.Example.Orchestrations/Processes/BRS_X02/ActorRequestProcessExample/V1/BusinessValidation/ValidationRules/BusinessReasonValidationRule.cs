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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.ActorRequestProcessExample.V1.Model;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.ActorRequestProcessExample.V1.BusinessValidation.ValidationRules;

public class BusinessReasonValidationRule : IBusinessValidationRule<ActorRequestProcessExampleInputV1>
{
    internal const string InvalidBusinessReason = "";
    internal const string ValidationErrorMessage = "BusinessReason skal være udfyldt / BusinessReaon must have a value";

    private IList<ValidationError> NoErrors => [];

    private IList<ValidationError> StringIsEmptyError => [
        new(
            Message: ValidationErrorMessage,
            ErrorCode: "E01"),
    ];

    public Task<IList<ValidationError>> ValidateAsync(ActorRequestProcessExampleInputV1 subject)
    {
        if (string.IsNullOrEmpty(subject.BusinessReason))
            return Task.FromResult(StringIsEmptyError);

        return Task.FromResult(NoErrors);
    }
}
