CREATE TABLE LicensePackages (
    PackageId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    SupplierId INT NOT NULL,
    ProductId UNIQUEIDENTIFIER NOT NULL,
    Quantity INT NOT NULL CHECK (Quantity > 0),
    PricePerUnit DECIMAL(12, 2) NOT NULL CHECK (PricePerUnit >= 0),
    ImportedToStock INT NOT NULL DEFAULT 0,
    EffectiveDate DATETIME2(3) NULL,
    CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    Notes NVARCHAR(500) NULL,

    CONSTRAINT FK_LicensePackages_Supplier FOREIGN KEY (SupplierId)
        REFERENCES Suppliers(SupplierId) ON DELETE CASCADE,

    CONSTRAINT FK_LicensePackages_Product FOREIGN KEY (ProductId)
        REFERENCES Products(ProductId) ON DELETE CASCADE
);

CREATE INDEX IX_LicensePackages_Supplier ON LicensePackages(SupplierId);
CREATE INDEX IX_LicensePackages_Product ON LicensePackages(ProductId);
CREATE INDEX IX_LicensePackages_CreatedAt ON LicensePackages(CreatedAt DESC);

GO

ALTER TABLE ProductKeys
    ADD SupplierId INT NOT NULL;
ALTER TABLE ProductKeys
    ADD CONSTRAINT FK_ProductKeys_Supplier
        FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierId);

CREATE INDEX IX_ProductKeys_Supplier ON ProductKeys(SupplierId);

GO

ALTER TABLE ProductKeys
    ADD Type VARCHAR(20) NOT NULL DEFAULT 'Individual';

GO

ALTER TABLE ProductKeys
    ADD CONSTRAINT CK_ProductKeys_Type CHECK (Type IN ('Individual', 'Pool'));

GO

ALTER TABLE ProductKeys
    ADD ExpiryDate DATETIME2(3) NULL;

GO

ALTER TABLE ProductKeys
    ADD Notes NVARCHAR(1000) NULL;

GO

ALTER TABLE ProductKeys
    ADD AssignedToOrderId UNIQUEIDENTIFIER NULL;

GO

ALTER TABLE ProductKeys
    ADD UpdatedAt DATETIME2(3) NULL;

GO

ALTER TABLE Suppliers
    ADD Status VARCHAR(20) NOT NULL DEFAULT 'Active';
GO

ALTER TABLE Suppliers
    ADD CONSTRAINT CK_Suppliers_Status CHECK (Status IN ('Active', 'Deactive'));

GO

ALTER TABLE Suppliers
    ADD LicenseTerms NVARCHAR(500) NULL;

GO

ALTER TABLE Products DROP CONSTRAINT FK_Products_Supplier;
GO

ALTER TABLE Products DROP COLUMN SupplierId;
GO