ALTER TABLE [pm].[OrchestrationInstance]
    ADD [Lifecycle_CreatedBy_ActorNumber]   NVARCHAR(16) NULL,
        [Lifecycle_CreatedBy_ActorRole]     NVARCHAR(50) NULL,
        [Lifecycle_CanceledBy_ActorNumber]  NVARCHAR(16) NULL,
        [Lifecycle_CanceledBy_ActorRole]    NVARCHAR(50) NULL
GO
