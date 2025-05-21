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
        EnergySupplierActorNumber = GetValueFromConfiguration(Root, "ENERGY_SUPPLIER_ACTOR_NUMBER");
        GridAccessProviderActorNumber = GetValueFromConfiguration(Root, "GRID_ACCESS_PROVIDER_ACTOR_NUMBER");

        var sharedKeyVaultName = GetValueFromConfiguration(Root, "SHARED_KEYVAULT_NAME");
        var internalKeyVaultName = GetValueFromConfiguration(Root, "INTERNAL_KEYVAULT_NAME");

        var keyVaultConfiguration = GetKeyVaultConfiguration(sharedKeyVaultName, internalKeyVaultName);

        ServiceBusNamespace = GetValueFromConfiguration(keyVaultConfiguration, "sb-domain-relay-namespace-endpoint");
        ProcessManagerStartTopicName = GetValueFromConfiguration(keyVaultConfiguration, "sbt-processmanagerstart-name");
        ProcessManagerNotifyTopicName = GetValueFromConfiguration(keyVaultConfiguration, "sbt-processmanagernotify-name");
        ProcessManagerBrs021StartTopicName = GetValueFromConfiguration(keyVaultConfiguration, "sbt-brs021forwardmetereddatastart-name");
        ProcessManagerBrs021NotifyTopicName = GetValueFromConfiguration(keyVaultConfiguration, "sbt-brs021forwardmetereddatanotify-name");

        // /.default must be added when running in the CD, else we get the following error: "Client credential flows
        // must have a scope value with /.default suffixed to the resource identifier (application ID URI)"
        ProcessManagerApplicationIdUri = GetValueFromConfiguration(keyVaultConfiguration, "processmanager-application-id-uri") + "/.default";
        ProcessManagerGeneralApiBaseAddress = GetValueFromConfiguration(keyVaultConfiguration, "func-api-pmcore-base-url");
        ProcessManagerOrchestrationsApiBaseAddress = GetValueFromConfiguration(keyVaultConfiguration, "func-orchestrations-pmorch-base-url");

        ProcessManagerDatabricksWorkspaceUrl = GetValueFromConfiguration(keyVaultConfiguration, "dbw-workspace-https-url");
        ProcessManagerDatabricksToken = GetValueFromConfiguration(keyVaultConfiguration, "dbw-workspace-token");
        ProcessManagerDatabricksSqlWarehouseId = GetValueFromConfiguration(keyVaultConfiguration, "process-manager-warehouse-id");

        EventHubNamespace = GetValueFromConfiguration(keyVaultConfiguration, "evhns-subsystemrelay-name") + ".servicebus.windows.net";
        ProcessManagerEventHubName = GetValueFromConfiguration(keyVaultConfiguration, "evh-brs021forwardmetereddatanotify-name");
    }

    public string EnergySupplierActorNumber { get; }

    public string GridAccessProviderActorNumber { get; }

    public string ServiceBusNamespace { get; }

    public string ProcessManagerStartTopicName { get; }

    public string ProcessManagerNotifyTopicName { get; }

    public string ProcessManagerBrs021StartTopicName { get; }

    public string ProcessManagerBrs021NotifyTopicName { get; }

    public string ProcessManagerApplicationIdUri { get; }

    public string ProcessManagerGeneralApiBaseAddress { get; }

    public string ProcessManagerOrchestrationsApiBaseAddress { get; }

    public string ProcessManagerDatabricksWorkspaceUrl { get; }

    public string ProcessManagerDatabricksToken { get; }

    public string ProcessManagerDatabricksSqlWarehouseId { get; }

    public string EventHubNamespace { get; }

    public string ProcessManagerEventHubName { get; }

    /// <summary>
    /// Build configuration for loading settings from key vault secrets.
    /// </summary>
    private IConfigurationRoot GetKeyVaultConfiguration(string sharedKeyVaultName, string internalKeyVaultName)
    {
        var sharedKeyVaultUrl = $"https://{sharedKeyVaultName}.vault.azure.net/";
        var internalKeyVaultUrl = $"https://{internalKeyVaultName}.vault.azure.net/";

        return new ConfigurationBuilder()
            .AddAuthenticatedAzureKeyVault(sharedKeyVaultUrl)
            .AddAuthenticatedAzureKeyVault(internalKeyVaultUrl)
            .Build();
    }

    private string GetValueFromConfiguration(IConfigurationRoot configuration, string key)
    {
        return configuration.GetValue<string>(key)
               ?? throw new NullReferenceException($"Missing configuration value for {key}");
    }
}
