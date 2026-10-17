CREATE TABLE [dbo].[UserProfile]
(
    [UserID]           BIGINT        NOT NULL,
    [BirthDate]        DATE          NULL,
    [Sex]              TINYINT       NOT NULL CONSTRAINT [DF_UserProfile_Sex] DEFAULT (0),
    [HeightCm]         DECIMAL (5, 1) NULL,
    [WeightKg]         DECIMAL (5, 1) NULL,
    [MaxHeartRate]     SMALLINT      NULL,
    [SleepNeedMinutes] SMALLINT      NULL,
    [UpdatedAt]        DATETIME2 (0) NOT NULL CONSTRAINT [DF_UserProfile_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_UserProfile] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [FK_UserProfile_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_UserProfile_Sex] CHECK ([Sex] IN (0, 1, 2))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'What the scores need to know about the person: date of birth and sex (reference values, biological age), height and weight, and optional overrides for maximum heart rate and sleep need. Sex: 0 unspecified, 1 male, 2 female.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'UserProfile';
