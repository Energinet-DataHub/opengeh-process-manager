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
using Energinet.DataHub.Core.TestCommon.Xunit.Configuration;
using Microsoft.Extensions.Configuration;

namespace Energinet.DataHub.ProcessManager.SubsystemTests.Fixtures;

public class ProcessManagerSubsystemTestConfiguration : SubsystemTestConfiguration
{
    public ProcessManagerSubsystemTestConfiguration()
    {
        EnergySupplierActorNumber = Root.GetValue<string>("ENERGY_SUPPLIER_ACTOR_NUMBER")
            ?? throw new ArgumentNullException(nameof(EnergySupplierActorNumber), $"Missing configuration value for ENERGY_SUPPLIER_ACTOR_NUMBER");

        var sharedKeyVaultName = Root.GetValue<string>("SHARED_KEYVAULT_NAME")
                                ?? throw new NullReferenceException($"Missing configuration value for SHARED_KEYVAULT_NAME");

        var keyVaultConfiguration = GetKeyVaultConfiguration(sharedKeyVaultName);

        ServiceBusNamespace = keyVaultConfiguration.GetValue<string>("sb-domain-relay-namespace-endpoint")
                                       ?? throw new ArgumentNullException(nameof(ServiceBusNamespace), $"Missing configuration value for {nameof(ServiceBusNamespace)}");

        ProcessManagerStartTopicName = keyVaultConfiguration.GetValue<string>("sbt-processmanagerstart-name")
            ?? throw new ArgumentNullException(nameof(ProcessManagerStartTopicName), $"Missing configuration value for {nameof(ProcessManagerStartTopicName)}");

        ProcessManagerNotifyTopicName = keyVaultConfiguration.GetValue<string>("sbt-processmanagernotify-name")
            ?? throw new ArgumentNullException(nameof(ProcessManagerNotifyTopicName), $"Missing configuration value for {nameof(ProcessManagerNotifyTopicName)}");

        ProcessManagerBrs021StartTopicName = keyVaultConfiguration.GetValue<string>("sbt-brs021forwardmetereddatastart-name")
            ?? throw new ArgumentNullException(nameof(ProcessManagerBrs021StartTopicName), $"Missing configuration value for {nameof(ProcessManagerBrs021StartTopicName)}");

        ProcessManagerBrs021NotifyTopicName = keyVaultConfiguration.GetValue<string>("sbt-brs021forwardmetereddatanotify-name")
            ?? throw new ArgumentNullException(nameof(ProcessManagerBrs021NotifyTopicName), $"Missing configuration value for {nameof(ProcessManagerBrs021NotifyTopicName)}");

        ProcessManagerApplicationIdUri = keyVaultConfiguration.GetValue<string>("processmanager-application-id-uri")
            ?? throw new ArgumentNullException(nameof(ProcessManagerApplicationIdUri), $"Missing configuration value for {nameof(ProcessManagerApplicationIdUri)}");

        // /.default must be added when running in the CD, else we get the following error: "Client credential flows
        // must have a scope value with /.default suffixed to the resource identifier (application ID URI)"
        ProcessManagerApplicationIdUri = $"{ProcessManagerApplicationIdUri}/.default";

        ProcessManagerGeneralApiBaseAddress = keyVaultConfiguration.GetValue<string>("func-api-pmcore-base-url")
            ?? throw new ArgumentNullException(nameof(ProcessManagerGeneralApiBaseAddress), $"Missing configuration value for {nameof(ProcessManagerGeneralApiBaseAddress)}");

        ProcessManagerOrchestrationsApiBaseAddress = keyVaultConfiguration.GetValue<string>("func-orchestrations-pmorch-base-url")
            ?? throw new ArgumentNullException(nameof(ProcessManagerOrchestrationsApiBaseAddress), $"Missing configuration value for {nameof(ProcessManagerOrchestrationsApiBaseAddress)}");
    }

    public string EnergySupplierActorNumber { get; }

    public string ServiceBusNamespace { get; }

    public string ProcessManagerStartTopicName { get; }

    public string ProcessManagerNotifyTopicName { get; }

    public string ProcessManagerBrs021StartTopicName { get; }

    public string ProcessManagerBrs021NotifyTopicName { get; }

    public string ProcessManagerApplicationIdUri { get; }

    public string ProcessManagerGeneralApiBaseAddress { get; }

    public string ProcessManagerOrchestrationsApiBaseAddress { get; }

    /// <summary>
    /// Build configuration for loading settings from key vault secrets.
    /// </summary>
    private IConfigurationRoot GetKeyVaultConfiguration(string keyVaultName)
    {
        var keyVaultUrl = $"https://{keyVaultName}.vault.azure.net/";

        return new ConfigurationBuilder()
            .AddAuthenticatedAzureKeyVault(keyVaultUrl)
            .Build();
    }
}
