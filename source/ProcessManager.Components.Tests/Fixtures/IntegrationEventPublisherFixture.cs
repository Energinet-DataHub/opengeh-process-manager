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

using System.ComponentModel.DataAnnotations;
using Azure.Identity;
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
using NJsonSchema.Annotations;
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

        ListenerMock = new ServiceBusListenerMock(
            TestLogger,
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);
    }

    public static IntegrationTestConfiguration IntegrationTestConfiguration { get; set; } = null!;

    public ITestDiagnosticsLogger TestLogger { get; }

    public ServiceBusListenerMock ListenerMock { get; }

    [Required]
    public ServiceProvider? Provider { get; set; }

    [Required]
    public HttpClient? HttpClient { get; set; }

    [NotNull]
    private TestServer? Server { get; set; }

    public async Task InitializeAsync()
    {
        var (topicResource, subscriptionProperties) = await CreateServiceBusTopic();

        await ListenerMock.AddTopicSubscriptionListenerAsync(
            topicResource.Name,
            subscriptionProperties.SubscriptionName);

        var services = new ServiceCollection();
        ConfigureServices(services, topicResource);

        Provider = services.BuildServiceProvider();

        var webHostBuilder = CreateWebHostBuilder(topicResource);
        Server = new TestServer(webHostBuilder);

        HttpClient = Server.CreateClient();
    }

    public Task DisposeAsync()
    {
        Server?.Dispose();
        return Task.CompletedTask;
    }

    private IWebHostBuilder CreateWebHostBuilder(TopicResource topicResource)
    {
        return new WebHostBuilder()
            .ConfigureServices(services =>
            {
                ConfigureServices(services, topicResource);
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
    }

    private void ConfigureServices(
        IServiceCollection services,
        TopicResource topicResource)
    {
        var configurations = new Dictionary<string, string?>
        {
            [$"{ServiceBusNamespaceOptions.SectionName}:{nameof(ServiceBusNamespaceOptions.FullyQualifiedNamespace)}"]
                = IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            [$"{IntegrationEventTopicOptions.SectionName}:{nameof(IntegrationEventTopicOptions.Name)}"]
                = topicResource.Name,
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurations)
            .Build();

        services.AddScoped<IConfiguration>(_ => configuration);
        services.AddServiceBusClientForApplication(configuration);
        services.AddIntegrationEventPublisher(new DefaultAzureCredential());
    }

    private async Task<(TopicResource TopicResource, SubscriptionProperties SubscriptionProperties)> CreateServiceBusTopic()
    {
        var serviceBusResourceProvider = new ServiceBusResourceProvider(
            new TestDiagnosticsLogger(),
            IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace,
            IntegrationTestConfiguration.Credential);

        var topicBuilder = serviceBusResourceProvider
            .BuildTopic(TopicName);

        topicBuilder.AddSubscription(SubscriptionName);

        var topic = await topicBuilder.CreateAsync();
        var subscription = topic.Subscriptions
            .Single(x => x.SubscriptionName.Equals(SubscriptionName));

        return (topic, subscription);
    }
}
