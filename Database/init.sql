
IF DB_ID('SimpleCommerce') IS NULL
BEGIN
    CREATE DATABASE SimpleCommerce;
END
GO

USE SimpleCommerce;
GO

-- ===== Users =====
CREATE TABLE dbo.Users (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
    PasswordHash VARBINARY(256) NOT NULL,
    PasswordSalt VARBINARY(128) NOT NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

-- ===== Products =====
CREATE TABLE dbo.Products (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    Price DECIMAL(18,2) NOT NULL,
    ImageUrl NVARCHAR(500) NULL,
    StockQty INT NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE INDEX IX_Products_IsActive ON dbo.Products(IsActive);
GO

-- ===== Carts =====
CREATE TABLE dbo.Carts (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Carts PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Status TINYINT NOT NULL CONSTRAINT DF_Carts_Status DEFAULT 0, -- 0 Active, 1 CheckedOut
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Carts_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_Carts_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Carts_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
);
GO

CREATE UNIQUE INDEX UX_Carts_ActiveCartPerUser
ON dbo.Carts(UserId)
WHERE Status = 0;
GO

-- ===== CartItems =====
CREATE TABLE dbo.CartItems (
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CartItems PRIMARY KEY,
    CartId UNIQUEIDENTIFIER NOT NULL,
    ProductId UNIQUEIDENTIFIER NOT NULL,
    Quantity INT NOT NULL,
    UnitPriceAtAdd DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CartItems_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_CartItems_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_CartItems_Carts FOREIGN KEY (CartId) REFERENCES dbo.Carts(Id),
    CONSTRAINT FK_CartItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id),
    CONSTRAINT CK_CartItems_Quantity CHECK (Quantity > 0)
);
GO

CREATE UNIQUE INDEX UX_CartItems_Cart_Product ON dbo.CartItems(CartId, ProductId);
GO

-- ===== Seed some products =====
INSERT INTO dbo.Products (Id, Name, Description, Price, ImageUrl, StockQty, IsActive)
VALUES
(NEWID(), 'Wireless Mouse', 'Ergonomic wireless mouse', 15.99, NULL, 50, 1),
(NEWID(), 'Mechanical Keyboard', 'Blue switch mechanical keyboard', 49.99, NULL, 30, 1),
(NEWID(), 'USB-C Hub', '6-in-1 USB-C hub', 25.00, NULL, 40, 1),
(NEWID(), '1TB External Hard Drive', 'USB 3.0 portable drive for backups', 45000.00, NULL, 20, 1),
(NEWID(), 'Bluetooth Headphones', 'Over-ear headphones with noise isolation', 32000.00, NULL, 25, 1),
(NEWID(), 'Wireless Earbuds', 'True wireless earbuds with charging case', 28000.00, NULL, 40, 1),
(NEWID(), '27-inch Monitor', 'Full HD monitor for work and gaming', 115000.00, NULL, 10, 1),
(NEWID(), 'Webcam 1080p', 'HD webcam for meetings and streaming', 22000.00, NULL, 35, 1),
(NEWID(), 'Laptop Stand', 'Aluminum adjustable stand for laptops', 12000.00, NULL, 60, 1),
(NEWID(), 'Power Bank 20000mAh', 'Fast-charging portable battery pack', 18000.00, NULL, 50, 1);
GO