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

using NCrontab;

namespace Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;

/// <summary>
/// Durable Functions orchestration description.
/// It contains the information necessary to locate and execute a Durable Functions
/// orchestration
/// </summary>
public class OrchestrationDescription
{
    internal const string StepsPrivatePropertyName = nameof(_steps);
    private List<StepDescription> _steps;

    private string _recurringCronExpression;

    public OrchestrationDescription(
        OrchestrationDescriptionUniqueName uniqueName,
        bool canBeScheduled,
        string functionName)
    {
        Id = new OrchestrationDescriptionId(Guid.NewGuid());
        UniqueName = uniqueName;
        CanBeScheduled = canBeScheduled;
        FunctionName = functionName;
        ParameterDefinition = new();
        HostName = string.Empty;
        IsEnabled = true;

        _steps = [];

        _recurringCronExpression = string.Empty;
    }

    /// <summary>
    /// Used by Entity Framework
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // ReSharper disable once UnusedMember.Local -- Used by Entity Framework
    private OrchestrationDescription()
    {
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public OrchestrationDescriptionId Id { get; }

    /// <summary>
    /// Uniquely identifies a specific implementation of the orchestration.
    /// </summary>
    public OrchestrationDescriptionUniqueName UniqueName { get; }

    /// <summary>
    /// Specifies if the orchestration supports scheduling (recurring orchestrations
    /// are also scheduled).
    /// If <see langword="false"/> then the orchestration can only
    /// be started directly (on-demand) and doesn't support scheduling.
    /// </summary>
    public bool CanBeScheduled { get; set; }

    /// <summary>
    /// A five-part cron format that expresses how this orchestration should
    /// be scheduled for execution recurringly.
    /// See https://github.com/atifaziz/NCrontab/wiki/Crontab-Expression
    /// </summary>
    public string RecurringCronExpression
    {
        get
        {
            return _recurringCronExpression;
        }

        set
        {
            if (value != _recurringCronExpression)
            {
                if (value != string.Empty && CrontabSchedule.TryParse(value) == null)
                    throw new ArgumentOutOfRangeException($"Invalid cron value '{value}'. See https://github.com/atifaziz/NCrontab/wiki/Crontab-Expression for expected format.");

                _recurringCronExpression = value;
            }
        }
    }

    public bool IsRecurring => RecurringCronExpression != string.Empty;

    /// <summary>
    /// The name of the Durable Functions orchestration implementation.
    /// </summary>
    public string FunctionName { get; set; }

    /// <summary>
    /// Specifies if the orchestration is implemented as a Durable Function.
    /// </summary>
    public bool IsDurableFunction => !string.IsNullOrWhiteSpace(FunctionName);

    /// <summary>
    /// Defines the Durable Functions orchestration input parameter type.
    /// </summary>
    public ParameterDefinition ParameterDefinition { get; }

    /// <summary>
    /// Defines the steps the orchestration is going through, and which should be
    /// visible to the users (e.g. shown in the UI).
    /// </summary>
    public IReadOnlyCollection<StepDescription> Steps => _steps.AsReadOnly();

    /// <summary>
    /// Whether the orchestration description is under development. If true, then the orchestration register allows
    /// updating properties that otherwise would be considered breaking changes.
    /// </summary>
    public bool IsUnderDevelopment { get; set; }

    /// <summary>
    /// This is set by the framework when synchronizing with the orchestration register during startup.
    /// The name of the host where the orchestration is implemented.
    /// </summary>
    public string HostName { get; internal set; }

    /// <summary>
    /// This is set by the framework when synchronizing with the orchestration register during startup.
    /// Specifies if the orchestration is enabled and hence can be started.
    /// Can be used to disable obsolete orchestrations that we have removed from code,
    /// but which we cannot delete in the database because we still need the execution history.
    /// </summary>
    public bool IsEnabled { get; internal set; }

    /// <summary>
    /// RowVersion is generated by the database and used for optimistic concurrency. Must be retrieved when loading
    /// the entity from the database, since Entity Framework uses it to throw an exception if the entity has been updated.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=fluent-api
    /// </remarks>
    internal byte[]? RowVersion { get; }

    /// <summary>
    /// Factory method that ensures domain rules are obeyed when creating and adding a new
    /// step description.
    /// </summary>
    public void AppendStepDescription(string description, bool canBeSkipped = false, string skipReason = "")
    {
        if (canBeSkipped && string.IsNullOrWhiteSpace(skipReason))
            ArgumentException.ThrowIfNullOrWhiteSpace(skipReason);

        var step = new StepDescription(
            Id,
            description,
            sequence: GetNextSequence(),
            canBeSkipped,
            skipReason);

        _steps.Add(step);
    }

    /// <summary>
    /// Overwrite the current steps with a new set of steps.
    /// </summary>
    /// <param name="newDescriptionSteps"></param>
    internal void OverwriteSteps(List<StepDescription> newDescriptionSteps)
    {
        _steps = newDescriptionSteps;
    }

    /// <summary>
    /// Generate next sequence number for a new step.
    /// </summary>
    private int GetNextSequence()
        => Steps.Count + 1;
}
