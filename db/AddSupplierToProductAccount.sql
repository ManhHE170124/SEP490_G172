ALTER TABLE ProductAccount
ADD SupplierId INT NULL;

UPDATE ProductAccount 
SET SupplierId = 3

ALTER TABLE ProductAccount
ALTER COLUMN SupplierId INT NOT NULL;

ALTER TABLE ProductAccount
ADD CONSTRAINT FK_ProductAccount_Supplier
FOREIGN KEY (SupplierId) REFERENCES Supplier(SupplierId)
ON DELETE NO ACTION
ON UPDATE NO ACTION;

CREATE INDEX IX_ProductAccount_SupplierId ON ProductAccount(SupplierId);
