CREATE TABLE Jobs
(
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    JobType NVARCHAR(100) NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,

    Status INT NOT NULL,
    RetryCount INT NOT NULL,
    MaxRetries INT NOT NULL,

    ErrorMessage NVARCHAR(MAX) NULL,

    CreatedAtUtc DATETIME2 NOT NULL,
    StartedAtUtc DATETIME2 NULL,
    CompletedAtUtc DATETIME2 NULL,
    NextRetryAtUtc DATETIME2 NULL
);

CREATE INDEX IX_Jobs_Status
ON Jobs(Status);

CREATE INDEX IX_Jobs_CreatedAtUtc
ON Jobs(CreatedAtUtc);

CREATE INDEX IX_Jobs_Status_CreatedAtUtc
ON Jobs(Status, CreatedAtUtc);