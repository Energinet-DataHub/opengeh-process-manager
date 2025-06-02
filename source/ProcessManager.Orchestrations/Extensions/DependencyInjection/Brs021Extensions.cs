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

using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;

public static class Brs021Extensions
{
    /// <summary>
    /// Add required dependencies for BRS-021 Send Measurements.
    /// </summary>
    public static IServiceCollection AddBrs021(
        this IServiceCollection services)
    {
        services
            .AddOptions<AdditionalRecipientsSourceOptions>()
            .BindConfiguration(AdditionalRecipientsSourceOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddMeasurementsClient();
        services.AddScoped<IMeteringPointMasterDataProvider, MeteringPointMasterDataProvider>();
        services.AddTransient<ElectricityMarketViewsFactory>();
        services.AddScoped<MeteringPointReceiversProvider>();

        services.AddScoped<IAdditionalMeasurementsRecipientsProvider, ConstantAdditionalMeasurementsRecipientsProvider>(sp =>
        {
            var additionalRecipientsOptions = sp.GetRequiredService<IOptions<AdditionalRecipientsSourceOptions>>();
            var selectedSource = Enum.TryParse<ConstantAdditionalMeasurementsRecipientsProvider.AdditionalRecipientConstantSourceSelector>(additionalRecipientsOptions.Value.Source, true, out var result)
                ? result
                : ConstantAdditionalMeasurementsRecipientsProvider.AdditionalRecipientConstantSourceSelector.Empty;

            return new ConstantAdditionalMeasurementsRecipientsProvider(selectedSource);
        });

        // Used by BRS-021 ForwardMeteredData process
        services.AddScoped<DelegationProvider>();
        services.AddScoped<TerminateForwardMeteredDataHandlerV1>();
        services.AddScoped<EnqueueMeasurementsHandlerV1>();

        return services;
    }
}
