UPDATE [pm].[OrchestrationInstance]
    SET [Lifecycle_CreatedBy_ActorNumber] = '0000000000000001',
        [Lifecycle_CreatedBy_ActorRole]   = 'SystemOperator'
    WHERE [Lifecycle_CreatedBy_ActorId] IS NOT NULL
GO
