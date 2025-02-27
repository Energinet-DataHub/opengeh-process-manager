IF EXISTS (SELECT * FROM sys.columns
           WHERE Name = N'IsDurableFunction' AND Object_ID = Object_ID(N'[pm].[OrchestrationDescription]'))
BEGIN
    ALTER TABLE [pm].[OrchestrationDescription]
        DROP COLUMN IsDurableFunction;
END
GO