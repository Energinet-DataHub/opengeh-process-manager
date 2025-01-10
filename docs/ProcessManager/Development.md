# Process Manager: Developer Handbook

In the following we will give an introduction to the `process manager`, some guidelines and how to get started.
If one wants a more abstract introduction, then please consult the documentation
in [Confluence](https://energinet.atlassian.net/wiki/spaces/D3/pages/1126072346/Analyse+og+design+til+PM-22+ProcessManager#ProcessManager-and-framework-design)
or [Miro](https://miro.com/app/board/uXjVLXgfr7o=/)

## Active feature flags

We use
the [built in functionality](https://learn.microsoft.com/en-us/azure/azure-functions/disable-function?tabs=portal) to
disable our `TimerTrigger`s used to manage scheduled and recurring orchestrations:

- `StartScheduledOrchestrationInstances`
- `PerformRecurringPlanning`

## Architecture

// TODO: Describe important principles, patterns
// TODO: Describe source code modularization -> static view
// TODO: Describe the flow -> "user" / runtime view
// TODO: Describe how artifacts are deployed -> deployment view

## Development

### Developing orchestrations

An orchestration is a durable function with activities.
We recommend that one follows the [guidelines for durable functions](https://energinet.atlassian.net/wiki/spaces/D3/pages/824475658/Durable+Functions).
Furthermore, we encourage people to create a new version of the orchestration if the orchestration is live and changed
to ensure that the history of the running orchestration are intact.
The previous versions may not run to completion if this happens,
consult [microsoft's versioning documentation for more information](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-versioning?tabs=csharp).
(The process manager supports the "side-by-side" Mitigation strategy).

As a rule of thumb, one should make a new version if one of the following is true:

- Input/output to the orchestration has changed
- Input/output to any activity has changed
- Any activity has been added or removed to the orchestration

### Developing activities

The activities should be idempotent and stateless.

Since the method `context.CallActivityAsync(...)` has no typechecking, we strongly advise that every activity has a
record with the inputs and outputs, e.g.:

```csharp
internal class SomeActivity_Brs_XYZ_V1(
    [FromKeyedServices(DatabricksWorkspaceNames.Measurements)] IDatabricksJobsClient client)
{
    private readonly ISomeClient _client;

    [Function(nameof(SomeActivity_Brs_XYZ_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput input)
    {

        var jobParameters = new List<string>
        {
            $"--orchestration-instance-id={input.InstanceId.Value}",
        };

        var jobId = await _client.StartJobAsync("ElectricalHeating", jobParameters).ConfigureAwait(false);
        return new ActivityOutput(jobId);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId);

    public record ActivityOutput(
        JobRunId jobId);
}
```

Such that the orchestration can invoke the activity like this:

```csharp
var activityOutput = await context.CallActivityAsync<SomeActivity_Brs_XYZ_V1.ActivityOutput>(
    nameof(SomeActivity_Brs_XYZ_V1),
    new SomeActivity_Brs_XYZ_V1.ActivityInput(
        instanceId),
    _defaultRetryOptions);
```

### Get started

The process manager may be started locally; swing by the secrets repository and get the necessary secrets.
To run the tests, one needs to fill out the `integrationtest.local.settings.json` file.
A sample file should be located in the root of the test project.

If one creates a new test project and wants it to be a part of CI, then this happens automatically.
A consequence of this is that every test project should end with `Tests`.

### Testing

Debugging the orchestration may be a troublesome task, since it lives in another process, and hence one has to manually
attach the debugger.
One can do this by setting a breakpoint in the test method just before the orchestration is started and then manually
attach the debugger to the required process, and continue the test.

### Recipe for implementation of a new process

// TODO: Describe how to implement the handling of a new process

## GitHub Workflows

// TODO: What is the purpose of each workflow; what is it's trigger; what is its "output"

## Structure of the solution

The process manager is a multipurpose tool, and thus has a strict structure.
If one opens the solution, one will notice that it consists of seven different solution folders:

- [`1. Client`](#solution-folder-1-client),
- [`2. Api`](#solution-folder-2-api),
- [`3. Core`](#solution-folder-3-core),
- [`4. Orchestrations`](#solution-folder-4-orchestrations),
- [`5. Components`](#solution-folder-5-components),
- [`6. SubsystemTests`](#solution-folder-6-subsystemtests),
- [`7. Examples`](#solution-folder-7-examples).

Where folder 1, 2, 3, and 7 are meant for the team maintaining the client and 4, 5, and 6 are for business developers.

### Solution folder 1. Client

The `1. Client` folder contains the `Client` which is used to administrate the orchestrations from other subsystems.
It is built as a nuget package containing a `HttpClient` and a `ServiceBusClient`.
Furthermore, this package contains the classes needed to communicate via the endpoints in
[`2. Api`](#solution-folder-2-api).

[`4. Orchestrations`](#solution-folder-4-orchestrations) contains an `Abstraction` solution as well,
which is released as a nuget package and used by the `Client` to communicate with the orchestrations via the custom `HttpsTriggers`.

### Solution folder 2. Api

The `2. Api` folder contains the `HttpTrigger` functions which are used to administrate the orchestrations.
It supports some basic functionality, such as starting an orchestration, checking the status of an orchestration and
canceling an orchestration.
Please note, that `Start` in the `Api` solution is restricted to orchestrations without an input.
If one wants to start an orchestration with input, one needs to implement a new `HttpTrigger` function in
[`4. Orchestrations`](#solution-folder-4-orchestrations).

### Solution folder 3. Core

The `3. Core` folder contains the database migrations and the communication with the database.
It's additionally responsible for registering the `Triggers` defined in
[`4. Orchestrations`](#solution-folder-4-orchestrations) and the `OrchestrationDescriptions` defined in the same
solution folder.

### Solution folder 4. Orchestrations

The projects inside solution folder `4. Orchestations` follows a strict folder structure, namely the following:

```text
├── Processes
│   ├── BRS_021
│   │   ├── ElectricalHeatingCalculation
|   |   |   |── V1
|   |   |   |   |── Activities
|   |   |   |   |── ...
|   |   |   |   |── Orchestration_Brs_021_ElectricalHeatingCalculation_V1.cs
|   |   |   |   └── OrchestrationDescription.cs
|   |   |   └── SearchElectricalHeatingCalculationHandler.cs
|   |   └── ForwardMeteredData
|   |       └── V1
|   |           |── Activities
|   |           └── OrchestrationDescription.cs
│   └── BRS_XYZ
|       └── V1
|           |── Activities
|           └── OrchestrationDescription.cs
```

With this structure, we're able to assign the business teams ownership of their respected processes, and consequently we
may avoid unintentional errors caused by mingling the responsibility and logic of the processes.

Every orchestration consists of a [durable function](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview?tabs=in-process%2Cnodejs-v3%2Cv1-model&pivots=csharp)
which should be post-fixed with the corresponding folder structure, as in the example above:
`Orchestration_Brs_021_ElectricalHeatingCalculation_V1.cs`.

Consult the example orchestrations in [`7.Example`](#solution-folder-7-examples) for inspiration.

### Solution folder 5. Components

All functionality that may be used by more than one process should be placed here.
This could be a client to `databricks` or the like.

### Solution folder 6. SubsystemTests

This is currently without functionality.

### Solution folder 7. Examples

The purpose of `7.Example` is to give inspiration of how one may implement orchestrations in the process manager.
Notice how the structure is a simplified version of folder [`4. Orchestrations`](#solution-folder-4-orchestrations).
The `Example Orchestration` contains two examples, with and without input. These can be used as inspiration to get up
and running.

The tests document how one may start an orchestration, how one may test the orchestration's activities, and how one may
use the client to check the orchestration status.

Besides that, the `Example orchestation` is used to test the client (solution folder 1, 2, and 3)
