CREATE TABLE [dbo].[AppSecret]
(
    [Name]      NVARCHAR (50)   NOT NULL,
    [Value]     NVARCHAR (1000) NOT NULL,
    [CreatedAt] DATETIME2 (0)   NOT NULL CONSTRAINT [DF_AppSecret_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_AppSecret] PRIMARY KEY CLUSTERED ([Name] ASC)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Values the server makes once and keeps: "push-vapid", the key pair push notifications are signed with (browsers subscribed with its public key, so it must survive rebuilds).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'AppSecret';
