-- This index covers the 2 SearchAsync methods in the OrchestrationInstanceRepository.
-- If the search methods are updated, this index should also be updated.
CREATE NONCLUSTERED INDEX IX_OrchestrationInstance_SearchComposite
    ON [pm].[OrchestrationInstance](
        [OrchestrationDescriptionId],
        [Lifecycle_CreatedBy_ActorNumber],
        [Lifecycle_CreatedBy_ActorRole],
        [Lifecycle_State],
        [Lifecycle_TerminationState],
        [Lifecycle_QueuedAt],
        [Lifecycle_ScheduledToRunAt],
        [Lifecycle_StartedAt],
        [Lifecycle_TerminatedAt]
    );
GO
