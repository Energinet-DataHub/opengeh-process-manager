-- Check and add RowVersion column to OrchestrationDescription table
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE Name = N'RowVersion' AND Object_ID = Object_ID(N'[pm].[OrchestrationDescription]'))
BEGIN
    ALTER TABLE [pm].[OrchestrationDescription]
        -- ROWVERSION makes Entity Framework throw an exception if trying to update a row which has already been updated (concurrency conflict)
        -- https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=fluent-api
        ADD [RowVersion] ROWVERSION NOT NULL
END
GO

-- Check and add RowVersion column to OrchestrationInstance table
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE Name = N'RowVersion' AND Object_ID = Object_ID(N'[pm].[OrchestrationInstance]'))
BEGIN
    ALTER TABLE [pm].[OrchestrationInstance]
        -- ROWVERSION makes Entity Framework throw an exception if trying to update a row which has already been updated (concurrency conflict)
        -- https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=fluent-api
        ADD [RowVersion] ROWVERSION NOT NULL
END
GO
