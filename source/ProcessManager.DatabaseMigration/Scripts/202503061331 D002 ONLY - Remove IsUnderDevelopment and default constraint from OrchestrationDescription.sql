-- Remove IsUnderDevelopment and default constraint from OrchestrationDescription on D002 ONLY
-- DELETE AFTER DEPLOYMENT TO D002

ALTER TABLE [pm].[OrchestrationDescription]
    DROP CONSTRAINT [DF__Orchestra__IsUnd__03F0984C];
GO

ALTER TABLE [pm].[OrchestrationDescription]
    DROP COLUMN [IsUnderDevelopment];
GO
