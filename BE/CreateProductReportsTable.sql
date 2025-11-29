-- Create ProductReports table
CREATE TABLE ProductReports (
    Id UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
    [Status] VARCHAR(20) NOT NULL DEFAULT 'Pending',
    [Name] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(2000) NOT NULL,
    ProductKeyId UNIQUEIDENTIFIER NULL,
    ProductAccountId UNIQUEIDENTIFIER NULL,
    ProductVariantId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(3) NULL,

    CONSTRAINT PK__ProductR__3214EC07A1B2C3D4 PRIMARY KEY (Id),

    -- Status check constraint (Pending, Processing, Resolved, Error)
    CONSTRAINT CK_ProductReports_Status CHECK ([Status] IN ('Pending', 'Processing', 'Resolved')),

    -- Foreign key constraints
    CONSTRAINT FK_ProductReports_ProductKey FOREIGN KEY (ProductKeyId)
        REFERENCES ProductKeys(KeyId) ON DELETE CASCADE,

    CONSTRAINT FK_ProductReports_ProductAccount FOREIGN KEY (ProductAccountId)
        REFERENCES ProductAccounts(ProductAccountId) ON DELETE CASCADE,

    CONSTRAINT FK_ProductReports_ProductVariant FOREIGN KEY (ProductVariantId)
        REFERENCES ProductVariants(VariantId) ON DELETE NO ACTION,

    CONSTRAINT FK_ProductReports_User FOREIGN KEY (UserId)
        REFERENCES Users(UserId) ON DELETE NO ACTION
);
GO

-- Create indexes for better query performance
CREATE INDEX IX_ProductReports_Status ON ProductReports([Status]);
GO

CREATE INDEX IX_ProductReports_User ON ProductReports(UserId);
GO

CREATE INDEX IX_ProductReports_Variant ON ProductReports(ProductVariantId);
GO

CREATE INDEX IX_ProductReports_CreatedAt ON ProductReports(CreatedAt DESC);
GO
