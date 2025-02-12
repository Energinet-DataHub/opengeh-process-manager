UPDATE [pm].[OrchestrationInstance]
    SET [Lifecycle_CanceledBy_ActorNumber] = '0000000000000001',
        [Lifecycle_CanceledBy_ActorRole]   = 'SystemOperator'
    WHERE [Lifecycle_CanceledBy_ActorId] IS NULL
GO
