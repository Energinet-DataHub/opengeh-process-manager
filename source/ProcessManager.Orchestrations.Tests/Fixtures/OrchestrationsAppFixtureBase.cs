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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.FunctionAppHost;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Tests.Fixtures;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

/// <summary>
/// Support testing Process Manager Orchestrations app and specifying configuration using inheritance.
/// This allows us to use multiple fixtures and coordinate their configuration.
/// </summary>
public abstract class OrchestrationsAppFixtureBase : IAsyncLifetime
{
    private readonly bool _disposeDatabase;

    public OrchestrationsAppFixtureBase(
        ProcessManagerDatabaseManager databaseManager,
        string taskHubName,
        int port,
        bool disposeDatabase = true)
    {
        DatabaseManager = databaseManager
            ?? throw new ArgumentNullException(nameof(databaseManager));
        TaskHubName = string.IsNullOrWhiteSpace(taskHubName)
            ? throw new ArgumentException("Cannot be null or whitespace.", nameof(taskHubName))
            : taskHubName;
        Port = port;
        _disposeDatabase = disposeDatabase;
        TestLogger = new TestDiagnosticsLogger();
        IntegrationTestConfiguration = new IntegrationTestConfiguration();

        AzuriteManager = new AzuriteManager(useOAuth: true);

        HostConfigurationBuilder = new FunctionAppHostConfigurationBuilder();
    }

    public ITestDiagnosticsLogger TestLogger { get; }

    public ProcessManagerDatabaseManager DatabaseManager { get; }

    [NotNull]
    public FunctionAppHostManager? AppHostManager { get; private set; }

    /// <summary>
    /// Durable Functions Task Hub Name
    /// See naming constraints: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-task-hubs?tabs=csharp#task-hub-names
    /// </summary>
    private string TaskHubName { get; }

    private int Port { get; }

    private IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    private AzuriteManager AzuriteManager { get; }

    private FunctionAppHostConfigurationBuilder HostConfigurationBuilder { get; }

    public async Task InitializeAsync()
    {
        // Clean up old Azurite storage
        CleanupAzuriteStorage();

        // Storage emulator
        AzuriteManager.StartAzurite();

        // Database
        await DatabaseManager.CreateDatabaseAsync();

        // Prepare host settings
        var appHostSettings = CreateAppHostSettings("ProcessManager.Orchestrations");

        // Create and start host
        AppHostManager = new FunctionAppHostManager(appHostSettings, TestLogger);
        StartHost(AppHostManager);
    }

    public async Task DisposeAsync()
    {
        AppHostManager.Dispose();
        AzuriteManager.Dispose();

        if (_disposeDatabase)
            await DatabaseManager.DeleteDatabaseAsync();
    }

    /// <summary>
    /// Use this method to attach <paramref name="testOutputHelper"/> to the host logging pipeline.
    /// While attached, any entries written to host log pipeline will also be logged to xUnit test output.
    /// It is important that it is only attached while a test i active. Hence, it should be attached in
    /// the test class constructor; and detached in the test class Dispose method (using 'null').
    /// </summary>
    /// <param name="testOutputHelper">If a xUnit test is active, this should be the instance of xUnit's <see cref="ITestOutputHelper"/>;
    /// otherwise it should be 'null'.</param>
    public void SetTestOutputHelper(ITestOutputHelper testOutputHelper)
    {
        TestLogger.TestOutputHelper = testOutputHelper;
    }

    private static void StartHost(FunctionAppHostManager hostManager)
    {
        IEnumerable<string> hostStartupLog;

        try
        {
            hostManager.StartHost();
        }
        catch (Exception)
        {
            // Function App Host failed during startup.
            // Exception has already been logged by host manager.
            hostStartupLog = hostManager.GetHostLogSnapshot();

            if (Debugger.IsAttached)
                Debugger.Break();

            // Rethrow
            throw;
        }

        // Function App Host started.
        hostStartupLog = hostManager.GetHostLogSnapshot();
    }

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private FunctionAppHostSettings CreateAppHostSettings(string csprojName)
    {
        var buildConfiguration = GetBuildConfiguration();

        var appHostSettings = HostConfigurationBuilder.CreateFunctionAppHostSettings();
        appHostSettings.FunctionApplicationPath = $"..\\..\\..\\..\\{csprojName}\\bin\\{buildConfiguration}\\net8.0";
        appHostSettings.Port = Port;

        // It seems the host + worker is not ready if we use the default startup log message, so we override it here
        appHostSettings.HostStartedEvent = "Host lock lease acquired";

        appHostSettings.ProcessEnvironmentVariables.Add(
            "FUNCTIONS_WORKER_RUNTIME",
            "dotnet-isolated");
        appHostSettings.ProcessEnvironmentVariables.Add(
            "AzureWebJobsStorage",
            AzuriteManager.FullConnectionString);
        appHostSettings.ProcessEnvironmentVariables.Add(
            "APPLICATIONINSIGHTS_CONNECTION_STRING",
            IntegrationTestConfiguration.ApplicationInsightsConnectionString);

        // ProcessManager
        // => Task Hub
        appHostSettings.ProcessEnvironmentVariables.Add(
            nameof(ProcessManagerTaskHubOptions.ProcessManagerStorageConnectionString),
            AzuriteManager.FullConnectionString);
        appHostSettings.ProcessEnvironmentVariables.Add(
            nameof(ProcessManagerTaskHubOptions.ProcessManagerTaskHubName),
            TaskHubName);
        // => Database
        appHostSettings.ProcessEnvironmentVariables.Add(
            $"{ProcessManagerOptions.SectionName}__{nameof(ProcessManagerOptions.SqlDatabaseConnectionString)}",
            DatabaseManager.ConnectionString);

        return appHostSettings;
    }

    /// <summary>
    /// Cleanup Azurite storage to avoid situations where Durable Functions
    /// would otherwise continue working on old orchestrations that e.g. failed in
    /// previous runs.
    /// </summary>
    private void CleanupAzuriteStorage()
    {
        if (Directory.Exists("__blobstorage__"))
            Directory.Delete("__blobstorage__", true);

        if (Directory.Exists("__queuestorage__"))
            Directory.Delete("__queuestorage__", true);

        if (Directory.Exists("__tablestorage__"))
            Directory.Delete("__tablestorage__", true);

        if (File.Exists("__azurite_db_blob__.json"))
            File.Delete("__azurite_db_blob__.json");

        if (File.Exists("__azurite_db_blob_extent__.json"))
            File.Delete("__azurite_db_blob_extent__.json");

        if (File.Exists("__azurite_db_queue__.json"))
            File.Delete("__azurite_db_queue__.json");

        if (File.Exists("__azurite_db_queue_extent__.json"))
            File.Delete("__azurite_db_queue_extent__.json");

        if (File.Exists("__azurite_db_table__.json"))
            File.Delete("__azurite_db_table__.json");

        if (File.Exists("__azurite_db_table_extent__.json"))
            File.Delete("__azurite_db_table_extent__.json");
    }
}
