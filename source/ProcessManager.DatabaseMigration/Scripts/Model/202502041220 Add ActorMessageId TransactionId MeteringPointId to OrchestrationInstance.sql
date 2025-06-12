ALTER TABLE [pm].[OrchestrationInstance]
    ADD [ActorMessageId]  NVARCHAR(36) NULL,
        [TransactionId]   NVARCHAR(36) NULL,
        [MeteringPointId] NVARCHAR(36) NULL
GO