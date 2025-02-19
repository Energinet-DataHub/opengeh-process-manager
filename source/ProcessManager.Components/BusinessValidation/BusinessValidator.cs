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

using Energinet.DataHub.ProcessManager.Components.Abstractions.BusinessValidation;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Components.BusinessValidation;

public class BusinessValidator<TInput>(
    ILogger<BusinessValidator<TInput>> logger,
    IEnumerable<IBusinessValidationRule<TInput>> validationRules)
        where TInput : IBusinessValidatedDto
{
    private readonly ILogger _logger = logger;
    private readonly IReadOnlyCollection<IBusinessValidationRule<TInput>> _validationRules = validationRules.ToList();

    /// <summary>
    /// Perform all validation rules for the given input.
    /// </summary>
    public async Task<IReadOnlyCollection<ValidationError>> ValidateAsync(TInput subject)
    {
        if (subject == null)
            throw new ArgumentNullException(nameof(subject));

        var errors = new List<ValidationError>();
        foreach (var rule in _validationRules)
        {
            errors.AddRange(await rule.ValidateAsync(subject).ConfigureAwait(false));
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning(
                "Validation failed for {SubjectType} type. Validation errors: {Errors}",
                subject.GetType().Name,
                errors);
        }

        return errors;
    }
}
