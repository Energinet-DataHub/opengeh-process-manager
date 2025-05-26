CREATE TABLE [pm].[SendMeasurementsInstance]
(
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [RowVersion]    ROWVERSION NOT NULL,

    [CreatedAt]             DATETIME2 NOT NULL,
    [CreatedByActorNumber]  VARCHAR(16) NOT NULL,
    [CreatedByActorRole]    TINYINT NOT NULL,

    [TransactionId]     VARCHAR(36) NOT NULL,
    [MeteringPointId]   VARCHAR(36) NULL, -- Is nullable since it is optional in the actor message. TODO: How many characters can a metering point id have?

    [MasterData]        VARCHAR(MAX) NULL,
    [ValidationErrors]  VARCHAR(MAX) NULL,

    [SentToMeasurementsAt]          DATETIME2 NULL,
    [ReceivedFromMeasurementsAt]    DATETIME2 NULL,

    [SentToEnqueueActorMessagesAt]          DATETIME2 NULL,
    [ReceivedFromEnqueueActorMessagesAt]    DATETIME2 NULL,

    [TerminatedAt]  DATETIME2 NULL,
    [FailedAt]      DATETIME2 NULL,
    [ErrorText]     VARCHAR(1000) NULL,

    -- A UNIQUE CLUSTERED constraint on an DATETIME2 column optimizes the performance of the table.
    -- by ordering indexes by the sequential DATETIME2 column instead of the UNIQUE IDENTIFIER primary key (which is random).
    -- The [Id] column is also needed if [CreatedAt] is the same for multiple rows, to ensure uniqueness.
    CONSTRAINT [PK_SendMeasurementsInstance] PRIMARY KEY NONCLUSTERED ([Id] ASC) WITH (
        PAD_INDEX = OFF,
        STATISTICS_NORECOMPUTE = OFF,
        IGNORE_DUP_KEY = OFF,
        ALLOW_ROW_LOCKS = ON,
        ALLOW_PAGE_LOCKS = ON),

    CONSTRAINT [UX_SendMeasurementsInstance_CreatedAt_Id] UNIQUE CLUSTERED ([CreatedAt] ASC, [Id] ASC) WITH (
        PAD_INDEX = OFF,
        STATISTICS_NORECOMPUTE = OFF,
        ALLOW_ROW_LOCKS = ON,
        ALLOW_PAGE_LOCKS = ON,
        FILLFACTOR = 90),
)
