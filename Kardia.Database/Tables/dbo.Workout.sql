CREATE TABLE [dbo].[Workout]
(
    [UserID]     BIGINT         NOT NULL,
    [StartedAt]  DATETIME2 (0)  NOT NULL,
    [EndedAt]    DATETIME2 (0)  NOT NULL,
    [Kind]       NVARCHAR (60)  NOT NULL,
    [ActiveKcal] DECIMAL (7, 1) NULL,
    [DistanceKm] DECIMAL (7, 2) NULL,
    [AvgBpm]     SMALLINT       NULL,
    [MaxBpm]     SMALLINT       NULL,
    [Hrr1]       SMALLINT       NULL,
    [Hrr2]       SMALLINT       NULL,
    CONSTRAINT [PK_Workout] PRIMARY KEY CLUSTERED ([UserID] ASC, [StartedAt] ASC),
    CONSTRAINT [FK_Workout_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Apple Watch workouts. A workout synced again (same start) replaces the old row. Hrr1/Hrr2: heart-rate recovery, the bpm the heart rate fell one and two minutes after the end, worked out from the heart-rate minutes.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Workout';
