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

using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Examples.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.NoInputExample;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.CustomQueries.Examples.V1;

internal static class ExamplesQueryV1Extensions
{
    public static IReadOnlyCollection<string> GetOrchestrationDescriptionNames(this ExamplesQueryV1 query)
    {
        var orchestrationDescriptionNames = query.ExampleTypes?
            .Select(type => GetOrchestrationDescriptionName(type))
            .Distinct()
            .ToList();

        if (orchestrationDescriptionNames == null)
            orchestrationDescriptionNames = [.. ExamplesQueryResultMapperV1.SupportedOrchestrationDescriptionNames];

        if (query.SkippedStepTwo.HasValue)
        {
            orchestrationDescriptionNames.RemoveAll(name
                => name != Brs_X01_InputExample.Name);
        }

        return orchestrationDescriptionNames;
    }

    private static string GetOrchestrationDescriptionName(ExampleTypeQueryParameterV1 exampleType)
    {
        switch (exampleType)
        {
            case ExampleTypeQueryParameterV1.Input:
                return Brs_X01_InputExample.Name;
            case ExampleTypeQueryParameterV1.NoInput:
                return Brs_X01_NoInputExample.Name;
            default:
                throw new InvalidOperationException($"Unsupported example type '{exampleType}'.");
        }
    }
}
