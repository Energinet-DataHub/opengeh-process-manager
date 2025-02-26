IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE Name = N'IsDurableFunction' AND Object_ID = Object_ID(N'[pm].[OrchestrationDescription]'))
BEGIN
    ALTER TABLE [pm].[OrchestrationDescription]
        ADD IsDurableFunction BIT NOT NULL CONSTRAINT D_OrchestrationDescription_IsDurableFunction DEFAULT 1;

    ALTER TABLE [pm].[OrchestrationDescription]
        DROP CONSTRAINT D_OrchestrationDescription_IsDurableFunction;
END
GO