# Process manager

## Intro

The process manager is responsible of orchestrating most process' in datahub. But it is not responsible of starting them.
This should be done via the other subsystems [EDI](https://github.com/Energinet-DataHub/opengeh-edi), [frontend](https://github.com/Energinet-DataHub/greenforce-frontend), [wholesale](https://github.com/Energinet-DataHub/opengeh-wholesale) and the like.
With this in mind, one should do all synchronous validations in the corresponding subsystem and not in the process manager, since the process manager is doing everything asynchronously.

## C4 Diagram of the process manager

![image](./docs/diagrams/c4-model/shitmanhelp)

### Description

A description of the C4 diagram can be found [here](https://energinet.atlassian.net/wiki/spaces/D3/pages/424476791/Domain+C4+Model)

### To update the c4 diagram

- Open visual studio Code
- hit ctrl + p -> an input field will appear in the top
- search for: `task structurizr lite: Load 'views'` and select the task which appears (This makes use of [docker](https://www.docker.com/))
- open a browser and go to: [localhost:8080/workspace/diagrams](http://localhost:8080/workspace/diagrams)

## Getting Started

The source code of the repository is provided as-is. We currently do not accept contributions or forks from outside the project driving the current development.

For people on the project please read the internal documentation (Confluence) for details on how to contribute or integrate with the subsystem.

## Where can I get more help?

Read about community for Green Energy Hub [here](https://github.com/Energinet-DataHub/green-energy-hub) and learn about how to get involved and get help.

## Thanks to all the people who already contributed

<a href="https://github.com/Energinet-DataHub/opengeh-proces-manager/graphs/contributors">
  <img src="https://contributors-img.web.app/image?repo=Energinet-DataHub/opengeh-process-manager" />
</a>
