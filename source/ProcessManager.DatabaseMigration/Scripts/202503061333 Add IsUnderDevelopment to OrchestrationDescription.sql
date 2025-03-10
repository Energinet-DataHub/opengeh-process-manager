ALTER TABLE [pm].[OrchestrationDescription]
    ADD [IsUnderDevelopment] BIT NOT NULL CONSTRAINT DF_OrchestrationDescription_IsUnderDevelopment DEFAULT 0;
