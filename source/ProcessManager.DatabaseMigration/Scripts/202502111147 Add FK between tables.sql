-- Add foreign key from StepInstance to OrchestrationInstance
IF NOT EXISTS (SELECT 1
               FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
               WHERE CONSTRAINT_NAME = 'FK_StepInstance_OrchestrationInstance')
BEGIN
ALTER TABLE [pm].[StepInstance]
    ADD CONSTRAINT FK_StepInstance_OrchestrationInstance
    FOREIGN KEY (OrchestrationInstanceId)
    REFERENCES [pm].[OrchestrationInstance](Id);
END
GO

-- Add foreign key from OrchestrationInstance to OrchestrationDescription
IF NOT EXISTS (SELECT 1
               FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
               WHERE CONSTRAINT_NAME = 'FK_OrchestrationInstance_OrchestrationDescription')
BEGIN
ALTER TABLE [pm].[OrchestrationInstance]
    ADD CONSTRAINT FK_OrchestrationInstance_OrchestrationDescription
    FOREIGN KEY (OrchestrationDescriptionId)
    REFERENCES [pm].[OrchestrationDescription](Id);
END
GO

-- Add foreign key from StepDescription to OrchestrationDescription
IF NOT EXISTS (SELECT 1
               FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
               WHERE CONSTRAINT_NAME = 'FK_StepDescription_OrchestrationDescription')
BEGIN
ALTER TABLE [pm].[StepDescription]
    ADD CONSTRAINT FK_StepDescription_OrchestrationDescription
    FOREIGN KEY (OrchestrationDescriptionId)
    REFERENCES [pm].[OrchestrationDescription](Id);
END
GO
