-- Check and add RowVersion column to StepDescription table
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE Name = N'RowVersion' AND Object_ID = Object_ID(N'[pm].[StepDescription]'))
BEGIN
    ALTER TABLE [pm].[StepDescription]
        -- ROWVERSION makes Entity Framework throw an exception if trying to update a row which has already been updated (concurrency conflict)
        -- https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=fluent-api
        ADD [RowVersion] ROWVERSION NOT NULL
END
GO

-- Check and add RowVersion column to StepInstance table
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE Name = N'RowVersion' AND Object_ID = Object_ID(N'[pm].[StepInstance]'))
BEGIN
    ALTER TABLE [pm].[StepInstance]
        -- ROWVERSION makes Entity Framework throw an exception if trying to update a row which has already been updated (concurrency conflict)
        -- https://learn.microsoft.com/en-us/ef/core/saving/concurrency?tabs=fluent-api
        ADD [RowVersion] ROWVERSION NOT NULL
END
GO
