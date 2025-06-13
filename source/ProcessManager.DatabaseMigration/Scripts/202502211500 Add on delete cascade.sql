
-- We alter tables to utilize DELETE CASCADE so steps will be deleted if their parent is deleted
ALTER TABLE [pm].[StepDescription]
    DROP CONSTRAINT [FK_StepDescription_OrchestrationDescription]
GO

ALTER TABLE [pm].[StepDescription]
    ADD CONSTRAINT [FK_StepDescription_OrchestrationDescription] FOREIGN KEY ([OrchestrationDescriptionId])
        REFERENCES [pm].[OrchestrationDescription]([Id])
        ON DELETE CASCADE
GO

ALTER TABLE [pm].[StepInstance]
    DROP CONSTRAINT [FK_StepInstance_OrchestrationInstance]
GO

ALTER TABLE [pm].[StepInstance]
    ADD CONSTRAINT [FK_StepInstance_OrchestrationInstance] FOREIGN KEY ([OrchestrationInstanceId])
        REFERENCES [pm].[OrchestrationInstance]([Id])
        ON DELETE CASCADE
GO