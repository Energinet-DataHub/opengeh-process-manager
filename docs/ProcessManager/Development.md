# Process Manager: Developer Handbook

The process manager is structure as a modular monolith, hence it has a strict structure.
If one opens the solution, one will notice that it consists of 7 different solution folder:

`1. Client`,
`2. Api`,
`3. Core`,
[`4. Orchestrations`](#solution-folder-4-orchestrations),
[`5. Components`](#solution-folder-5-components),
[`6. SubsystemTests`](#solution-folder-6-subsystemtests),
[`7. Examples`](#solution-folder-7-examples).

Where folder 1, 2, 3, 7 are meant for the team maintaining the client and  4, 5, 6 are for business developers.

## Solution folder 4. Orchestrations

The projects inside solution folder `4. Orchestations` follows a strict folder structure, namely the following:

```text
├── Process
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
```

With this structure we're able to assign the business teams ownership of the respected process,
hence we may avoid unintentional errors.

Every orchestration consists of a [durable function](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview?tabs=in-process%2Cnodejs-v3%2Cv1-model&pivots=csharp)
which should be post-fixed with the corresponding folder structure, as in the example above: `Orchestration_Brs_021_ElectricalHeatingCalculation_V1.cs`.

Consult the example orchestrations in [`7.Example`](#solution-folder-7-examples) for inspiration.

## Solution folder 5. Components

All functionality which may be used by more than one process should be placed here.
This could be a client to `databricks` or the like.

## Solution folder 6. SubsystemTests

This is currently without functionality.

## Active feature flags

We use TimerTrigger [built in functionality](https://learn.microsoft.com/en-us/azure/azure-functions/disable-function?tabs=portal) to disable them in preproduction and production.

The TimerTriggers are:

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
We recommend that one follows the [guidelines for durable functions](https://energinet.atlassian.net/wiki/spaces/D3/pages/824475658/Durable+Functions)
Furthermore, we encourage people to create a new version of the orchestration if the orchestration is live and changed.

### Testing

Debugging the orchestration may be a troublesome task. Since it lives in another process, hence one has to manually attach the debugger.
One can do this by setting a breakpoint in the test method just before the orchestration is started.
Then attach the debugger and continue the test.

### Get started

// TODO: Goal - How to run tests locally to verify the development environment is ready
// TODO: Goal - How to run relevant applications locally for debugging
// TODO: Goal - How to create your first PR and verify it in CI and CD

### Development Guidelines

// TODO: Describe or link to development guidelines for Durable Functions and Extensions (we have guidelines for at least these in Confluence)

### Recipe for implementation of new process

// TODO: Describe how to implement the handling of a new process

### Solution folder 7. Examples

The purpose of `7.Example` is to give inspiration of how may implement orchestrations in the `process manager`.
Notice how  the structure is a simplified version of folder [`4. Orchestrations`](#solution-folder-4-orchestrations).
The `Example Orchestration` contains two examples, with and without input. Which may be used as inspiration to get up and running.
The tests document how one may start an orchestration.
How one may test the orchestrations activities and how one may use the client to check the orchestration status.

Besides that, the `Example orchestation` is used to test the client (solution folder 1, 2, 3 )

## GitHub Workflows

// TODO: What is the purpose of each workflow; what is it's trigger; what is its "output"
