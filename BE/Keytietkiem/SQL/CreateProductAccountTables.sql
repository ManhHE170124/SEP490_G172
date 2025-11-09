CREATE TABLE ProductAccounts (
    ProductAccountId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,
    AccountEmail NVARCHAR(254) NOT NULL,
    AccountUsername NVARCHAR(100) NULL,
    AccountPassword NVARCHAR(512) NOT NULL,
    MaxUsers INT NOT NULL DEFAULT 1,
    Status VARCHAR(20) NOT NULL DEFAULT 'Active',
    ExpiryDate DATETIME2(3) NULL,
    Notes NVARCHAR(1000) NULL,
    CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    UpdatedAt DATETIME2(3) NOT NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,

    CONSTRAINT FK_ProductAccounts_Product FOREIGN KEY (ProductId)
        REFERENCES Products(ProductId),

    INDEX IX_ProductAccounts_Product (ProductId),
    INDEX IX_ProductAccounts_Status (Status)
);
GO
CREATE TABLE ProductAccountCustomers (
    ProductAccountCustomerId BIGINT PRIMARY KEY IDENTITY(1,1),
    ProductAccountId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    AddedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    AddedBy UNIQUEIDENTIFIER NOT NULL,
    RemovedAt DATETIME2(3) NULL,
    RemovedBy UNIQUEIDENTIFIER NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    Notes NVARCHAR(500) NULL,

    CONSTRAINT FK_ProductAccountCustomers_Account FOREIGN KEY (ProductAccountId)
        REFERENCES ProductAccounts(ProductAccountId),
    CONSTRAINT FK_ProductAccountCustomers_User FOREIGN KEY (UserId)
        REFERENCES Users(UserId),

    INDEX IX_ProductAccountCustomers_Account_User (ProductAccountId, UserId),
    INDEX IX_ProductAccountCustomers_User (UserId),
    INDEX IX_ProductAccountCustomers_Active (IsActive)
);
GO
CREATE TABLE ProductAccountHistories (
    HistoryId BIGINT PRIMARY KEY IDENTITY(1,1),
    ProductAccountId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NULL,
    Action VARCHAR(20) NOT NULL,
    ActionBy UNIQUEIDENTIFIER NOT NULL,
    ActionAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    Notes NVARCHAR(500) NULL,

    CONSTRAINT FK_ProductAccountHistory_Account FOREIGN KEY (ProductAccountId)
        REFERENCES ProductAccounts(ProductAccountId),
    CONSTRAINT FK_ProductAccountHistory_User FOREIGN KEY (UserId)
        REFERENCES Users(UserId),

    INDEX IX_ProductAccountHistory_Account (ProductAccountId),
    INDEX IX_ProductAccountHistory_User (UserId),
    INDEX IX_ProductAccountHistory_ActionAt (ActionAt DESC)
);
GO