ALTER TABLE [pm].[OrchestrationDescription]
    ADD [RecurringCronExpression] NVARCHAR(255) NULL
GO

UPDATE [pm].[OrchestrationDescription]
   SET [RecurringCronExpression] = ''
   WHERE [RecurringCronExpression] IS NULL
GO

ALTER TABLE [pm].[OrchestrationDescription]
    ALTER COLUMN [RecurringCronExpression] NVARCHAR(255) NOT NULL
GO