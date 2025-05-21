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

using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Application;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.CalculatedMeasurements.V1.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Options;

public class OptionsConfiguration : IOptionsConfiguration
{
    public IServiceCollection Configure(IServiceCollection services)
    {
        services
            .AddOptions<OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1>()
            .BindConfiguration(OrchestrationOptions_Brs_021_ElectricalHeatingCalculation_V1.SectionName)
            .ValidateDataAnnotations();

        return services;
    }
}
