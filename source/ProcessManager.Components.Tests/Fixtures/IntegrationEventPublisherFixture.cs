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

using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus.Administration;
using Energinet.DataHub.Core.App.WebApp.Extensions.Builder;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Configuration;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ResourceProvider;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.DependencyInjection;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.Core.TestCommon.Diagnostics;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Fixtures;

public sealed class IntegrationEventPublisherFixture : IAsyncLifetime
{
    private const string TopicName = "test-topic";
    private const string SubscriptionName = "test-subscription";

    public IntegrationEventPublisherFixture()
    {
        TestLogger = new TestDiagnosticsLogger();
        IntegrationTestConfiguration = new IntegrationTestConfiguration();

        IntegrationEventListenerMock = new ServiceBusListenerMock(
            TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);

        ServiceBusResourceProvider = new ServiceBusResourceProvider(
            TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
    }

    public IntegrationTestConfiguration IntegrationTestConfiguration { get; }

    public ITestDiagnosticsLogger TestLogger { get; }

    public ServiceBusListenerMock IntegrationEventListenerMock { get; }

    [NotNull]
    public ServiceProvider? Provider { get; set; }

    [NotNull]
    public HttpClient? HealthChecksHttpClient { get; set; }

    private ServiceBusResourceProvider ServiceBusResourceProvider { get; }

    [NotNull]
    private TestServer? HealthChecksWebServer { get; set; }

    [NotNull]
    private TopicResource? IntegrationEventTopic { get; set; }

    [NotNull]
    private SubscriptionProperties? IntegrationEventSubscription { get; set; }

    public async Task InitializeAsync()
    {
        var (topicResource, subscriptionProperties) = await CreateServiceBusTopic();
        IntegrationEventTopic = topicResource;
        IntegrationEventSubscription = subscriptionProperties;

        await IntegrationEventListenerMock.AddTopicSubscriptionListenerAsync(
            IntegrationEventTopic.Name,
            IntegrationEventSubscription.SubscriptionName);

        var services = new ServiceCollection();
        ConfigureServices(services);

        Provider = services.BuildServiceProvider();

        HealthChecksWebServer = CreateHealthChecksServer();
        HealthChecksHttpClient = HealthChecksWebServer.CreateClient();
    }

    public async Task DisposeAsync()
    {
        HealthChecksWebServer?.Dispose();
        HealthChecksHttpClient?.Dispose();

        await ServiceBusResourceProvider.DisposeAsync();
        await IntegrationEventListenerMock.DisposeAsync();
    }

    private TestServer CreateHealthChecksServer()
    {
        var webHostBuilder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                ConfigureServices(services);
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();

                app.UseEndpoints(endpoints =>
                {
                    // Health check is registered for "ready" endpoint
                    endpoints.MapReadyHealthChecks();
                });
            });

        return new TestServer(webHostBuilder);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var configurations = new Dictionary<string, string?>
        {
            [$"{ServiceBusNamespaceOptions.SectionName}:{nameof(ServiceBusNamespaceOptions.FullyQualifiedNamespace)}"]
                = IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            [$"{IntegrationEventTopicOptions.SectionName}:{nameof(IntegrationEventTopicOptions.Name)}"]
                = IntegrationEventTopic.Name,
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurations)
            .Build();

        services.AddScoped<IConfiguration>(_ => configuration);
        services.AddServiceBusClientForApplication(configuration);
        services.AddIntegrationEventPublisher(IntegrationTestConfiguration.Credential);
    }

    private async Task<(TopicResource TopicResource, SubscriptionProperties SubscriptionProperties)> CreateServiceBusTopic()
    {
        var topicBuilder = ServiceBusResourceProvider
            .BuildTopic(TopicName);

        topicBuilder.AddSubscription(SubscriptionName);

        var topic = await topicBuilder.CreateAsync();
        var subscription = topic.Subscriptions
            .Single(x => x.SubscriptionName.Equals(SubscriptionName));

        return (topic, subscription);
    }
}
