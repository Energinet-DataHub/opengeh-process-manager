IF NOT EXISTS (SELECT 1
               FROM sys.indexes
               WHERE name = 'IX_OrchestrationInstance_OrchestrationDescriptionId'
               AND object_id = OBJECT_ID('[pm].[OrchestrationInstance]'))
BEGIN
CREATE NONCLUSTERED INDEX IX_OrchestrationInstance_OrchestrationDescriptionId
    ON [pm].[OrchestrationInstance]([OrchestrationDescriptionId]);
END
GO

IF NOT EXISTS (SELECT 1
               FROM sys.indexes
               WHERE name = 'IX_StepInstance_OrchestrationInstanceId'
               AND object_id = OBJECT_ID('[pm].[StepInstance]'))
BEGIN
CREATE NONCLUSTERED INDEX IX_StepInstance_OrchestrationInstanceId
    ON [pm].[StepInstance]([OrchestrationInstanceId]);
END
GO