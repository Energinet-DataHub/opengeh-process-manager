ALTER TABLE [pm].[OrchestrationInstance]
    ADD [IdempotencyKey] NVARCHAR(1024) NULL
GO

CREATE UNIQUE NONCLUSTERED INDEX UX_OrchestrationInstance_IdempotencyKey
    ON [pm].[OrchestrationInstance]([IdempotencyKey])
    WHERE [IdempotencyKey] IS NOT NULL;
GO