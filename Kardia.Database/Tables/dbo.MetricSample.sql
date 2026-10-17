CREATE TABLE [dbo].[MetricSample]
(
    [UserID]   BIGINT          NOT NULL,
    [Metric]   TINYINT         NOT NULL,
    [SampleAt] DATETIME2 (0)   NOT NULL,
    [Value]    DECIMAL (10, 3) NOT NULL,
    CONSTRAINT [PK_MetricSample] PRIMARY KEY CLUSTERED ([UserID] ASC, [SampleAt] ASC, [Metric] ASC),
    CONSTRAINT [FK_MetricSample_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
) WITH (DATA_COMPRESSION = PAGE);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Timed readings of the values the Watch measures mostly while you sleep: HRV (1), respiratory rate (3), blood oxygen (4), wrist temperature (5). Their day value in DailyMetric is the average of the readings taken during that night, so readings later in the day never change the morning''s recovery.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'MetricSample';
