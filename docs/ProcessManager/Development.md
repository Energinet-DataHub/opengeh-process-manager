# Process Manager: Developer Handbook

// TODO: This ducoment should be split into multiple; this is just a placeholder for relevant subjects that should be documented.

## Active feature flags

We use TimerTrigger [built in functionality](https://learn.microsoft.com/en-us/azure/azure-functions/disable-function?tabs=portal) to disable them in preproduction and production.

The TimerTriggers are:

- `StartScheduledOrchestrationInstances`
- `PerformRecurringPlanning`

## Organization of artifacts

// TODO: Describe organization in repository -> files/folders

## Architecture

// TODO: Describe important principles, patterns
// TODO: Describe source code modularization -> static view
// TODO: Describe the flow -> "user" / runtime view
// TODO: Describe how artifacts are deployed -> deployment view

## Development

The `process manager` has 7 different folders.
Folder 1, 2, 3, 7 contains code to estalish communication between the subsystems and the process manager, without business logic.
And folder 4, 5, 6 contains the orchestrations where all business logic should be located.
With this in mind everything outside folder 4, 5, 6 are owned by team Mosaic.
Everything insde folder 4, 5, 6 may be assigned other teams, on a BRS level.

### Get started

// TODO: Goal - How to run tests locally to verify the development environment is ready
// TODO: Goal - How to run relevant applications locally for debugging
// TODO: Goal - How to create your first PR and verify it in CI and CD

### Development Guidelines

// TODO: Describe or link to development guidelines for Durable Functions and Extensions (we have guidelines for at least these in Confluence)

### Recipe for implementation of new process

// TODO: Describe how to implement the handling of a new process

### Example Orchestrations

The purpose of `7.Example` is to give inspiration of how may implement orchestrations in the `process manager`.
Notice how  the strcture is a simplifien version of folder `4.Orchestrations`.
The `Example Orchestration` contains two examples, with and without input. Which may be used as inspiration to get up and running.
The tests document how one may start an orchestration. How one may test the orchestrations activities and how one may use the client to check the orchestration status.

Besides that, the `Example orchestation` is used to test the client (Folder 1, 2, 3 )

## GitHub Workflows

// TODO: What is the purpose of each workflow; what is it's trigger; what is its "output"
