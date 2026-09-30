-- ============================================================================
-- MONSTERASP CLOUD DATABASE COMPLETE SETUP SCRIPT
-- Database: db70848
-- Server: db70848.databaseasp.net / db70848.public.databaseasp.net
-- Generated for Fixory CRM Computer Repair Cloud Storing & Sync
-- ============================================================================

-- MASTER & IDENTITY SCHEMA
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [Companies] (
        [CompanyId] int NOT NULL IDENTITY,
        [CompanyCode] nvarchar(50) NOT NULL,
        [CompanyName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([CompanyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [CompanyDatabases] (
        [CompanyDatabaseId] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [ServerName] nvarchar(200) NOT NULL,
        [DatabaseName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_CompanyDatabases] PRIMARY KEY ([CompanyDatabaseId]),
        CONSTRAINT [FK_CompanyDatabases_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE TABLE [Devices] (
        [DeviceId] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [DeviceCode] nvarchar(50) NOT NULL,
        [DeviceName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([DeviceId]),
        CONSTRAINT [FK_Devices_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Companies_CompanyCode] ON [Companies] ([CompanyCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompanyDatabases_CompanyId] ON [CompanyDatabases] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Devices_CompanyId_DeviceCode] ON [Devices] ([CompanyId], [DeviceCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260908143055_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260908143055_InitialCreate', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [Brand] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [DeviceType] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [Model] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [PurchaseDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [PurchasePrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [SerialNumber] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [Status] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [WarrantyExpiry] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [Devices] ADD [WarrantyStatus] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [CompanyDatabases] ADD [CredentialKey] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [FirstName] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LastName] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [AuditLogId] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NULL,
        [Action] nvarchar(100) NOT NULL,
        [Entity] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(max) NULL,
        [Details] nvarchar(2000) NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([AuditLogId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [Customers] (
        [CustomerId] int NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [Phone] nvarchar(50) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [LoyaltyPoints] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [LoyaltyPrograms] (
        [LoyaltyProgramId] int NOT NULL IDENTITY,
        [CompanyId] int NULL,
        [ProgramName] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [PointsPerPeso] int NOT NULL,
        [DiscountPercentage] decimal(5,2) NOT NULL,
        [MinimumSpend] decimal(18,2) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LoyaltyPrograms] PRIMARY KEY ([LoyaltyProgramId]),
        CONSTRAINT [FK_LoyaltyPrograms_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [Products] (
        [ProductId] int NOT NULL IDENTITY,
        [ProductCode] nvarchar(50) NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([ProductId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [Subscriptions] (
        [SubscriptionId] int NOT NULL IDENTITY,
        [CompanyId] int NULL,
        [SubscriptionName] nvarchar(200) NOT NULL,
        [PricePerMonth] decimal(18,2) NOT NULL,
        [MaxUsers] int NOT NULL,
        [MaxDevices] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [BillingCycle] nvarchar(50) NULL,
        CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([SubscriptionId]),
        CONSTRAINT [FK_Subscriptions_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([CompanyId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [TermsAndConditionsSet] (
        [TermsId] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Version] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedByUserId] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TermsAndConditionsSet] PRIMARY KEY ([TermsId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [RepairRequests] (
        [RepairRequestId] int NOT NULL IDENTITY,
        [RequestNumber] nvarchar(50) NOT NULL,
        [CustomerId] int NOT NULL,
        [DeviceId] int NULL,
        [DeviceModel] nvarchar(200) NOT NULL,
        [SerialNumber] nvarchar(100) NOT NULL,
        [IssueDescription] nvarchar(2000) NOT NULL,
        [Status] int NOT NULL,
        [Priority] int NOT NULL,
        [RequestDate] datetime2 NOT NULL,
        [CompletionDate] datetime2 NULL,
        [EstimatedCost] decimal(18,2) NULL,
        [ActualCost] decimal(18,2) NULL,
        [PartsCost] decimal(18,2) NULL,
        [LaborCost] decimal(18,2) NULL,
        [TechnicianNotes] nvarchar(2000) NULL,
        [AssignedToStaffId] nvarchar(max) NULL,
        [AssignedToManagerId] nvarchar(max) NULL,
        CONSTRAINT [PK_RepairRequests] PRIMARY KEY ([RepairRequestId]),
        CONSTRAINT [FK_RepairRequests_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RepairRequests_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([DeviceId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [CustomerLoyaltyAccounts] (
        [CustomerLoyaltyAccountId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [LoyaltyProgramId] int NOT NULL,
        [Points] int NOT NULL,
        [TotalSpent] decimal(18,2) NOT NULL,
        [JoinedDate] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_CustomerLoyaltyAccounts] PRIMARY KEY ([CustomerLoyaltyAccountId]),
        CONSTRAINT [FK_CustomerLoyaltyAccounts_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerLoyaltyAccounts_LoyaltyPrograms_LoyaltyProgramId] FOREIGN KEY ([LoyaltyProgramId]) REFERENCES [LoyaltyPrograms] ([LoyaltyProgramId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [CustomerInteractions] (
        [CustomerInteractionId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [RepairRequestId] int NULL,
        [InteractionType] int NOT NULL,
        [Notes] nvarchar(1000) NOT NULL,
        [InteractionDate] datetime2 NOT NULL,
        [InteractionByUserId] nvarchar(max) NULL,
        CONSTRAINT [PK_CustomerInteractions] PRIMARY KEY ([CustomerInteractionId]),
        CONSTRAINT [FK_CustomerInteractions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerInteractions_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [Payments] (
        [PaymentId] int NOT NULL IDENTITY,
        [RepairRequestId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [PaymentMethod] nvarchar(50) NULL,
        [ReferenceNumber] nvarchar(100) NULL,
        [IsPaid] bit NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [FK_Payments_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE TABLE [RepairStatusHistories] (
        [RepairStatusHistoryId] int NOT NULL IDENTITY,
        [RepairRequestId] int NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [ChangedByUserId] nvarchar(max) NULL,
        [Notes] nvarchar(500) NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RepairStatusHistories] PRIMARY KEY ([RepairStatusHistoryId]),
        CONSTRAINT [FK_RepairStatusHistories_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_CustomerInteractions_CustomerId] ON [CustomerInteractions] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_CustomerInteractions_RepairRequestId] ON [CustomerInteractions] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_CustomerLoyaltyAccounts_CustomerId] ON [CustomerLoyaltyAccounts] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_CustomerLoyaltyAccounts_LoyaltyProgramId] ON [CustomerLoyaltyAccounts] ([LoyaltyProgramId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_LoyaltyPrograms_CompanyId] ON [LoyaltyPrograms] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_Payments_RepairRequestId] ON [Payments] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_ProductCode] ON [Products] ([ProductCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_RepairRequests_CustomerId] ON [RepairRequests] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_RepairRequests_DeviceId] ON [RepairRequests] ([DeviceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RepairRequests_RequestNumber] ON [RepairRequests] ([RequestNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_RepairStatusHistories_RepairRequestId] ON [RepairStatusHistories] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    CREATE INDEX [IX_Subscriptions_CompanyId] ON [Subscriptions] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913101303_AddAllCrmEntities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913101303_AddAllCrmEntities', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerInteractions]') AND [c].[name] = N'Notes');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [CustomerInteractions] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [CustomerInteractions] ALTER COLUMN [Notes] nvarchar(2000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerInteractions]') AND [c].[name] = N'InteractionByUserId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [CustomerInteractions] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [CustomerInteractions] ALTER COLUMN [InteractionByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerInteractions]') AND [c].[name] = N'CustomerId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [CustomerInteractions] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [CustomerInteractions] ALTER COLUMN [CustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [ClosedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [Priority] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [Resolution] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [Status] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [Subject] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    CREATE TABLE [Supplier] (
        [SupplierId] int NOT NULL IDENTITY,
        [SupplierCode] nvarchar(max) NOT NULL,
        [SupplierName] nvarchar(max) NOT NULL,
        [ContactPerson] nvarchar(max) NULL,
        [ContactNumber] nvarchar(max) NULL,
        [EmailAddress] nvarchar(max) NULL,
        [Address] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Supplier] PRIMARY KEY ([SupplierId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    CREATE TABLE [Part] (
        [PartId] int NOT NULL IDENTITY,
        [PartCode] nvarchar(max) NOT NULL,
        [PartName] nvarchar(max) NOT NULL,
        [Category] nvarchar(max) NULL,
        [Manufacturer] nvarchar(max) NULL,
        [Model] nvarchar(max) NULL,
        [UnitCost] decimal(18,2) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [QuantityOnHand] int NOT NULL,
        [ReorderLevel] int NOT NULL,
        [SupplierId] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Part] PRIMARY KEY ([PartId]),
        CONSTRAINT [FK_Part_Supplier_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Supplier] ([SupplierId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    CREATE TABLE [RepairPart] (
        [RepairPartId] int NOT NULL IDENTITY,
        [RepairRequestId] int NOT NULL,
        [PartId] int NOT NULL,
        [QuantityUsed] int NOT NULL,
        [UnitCostAtTime] decimal(18,2) NOT NULL,
        [UnitPriceAtTime] decimal(18,2) NOT NULL,
        [UsedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RepairPart] PRIMARY KEY ([RepairPartId]),
        CONSTRAINT [FK_RepairPart_Part_PartId] FOREIGN KEY ([PartId]) REFERENCES [Part] ([PartId]) ON DELETE CASCADE,
        CONSTRAINT [FK_RepairPart_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    CREATE INDEX [IX_Part_SupplierId] ON [Part] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    CREATE INDEX [IX_RepairPart_PartId] ON [RepairPart] ([PartId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    CREATE INDEX [IX_RepairPart_RepairRequestId] ON [RepairPart] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115454_AddInteractionCrudFields_Master'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260915115454_AddInteractionCrudFields_Master', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915123210_AddFollowUpAndInteractionFields_Master'
)
BEGIN
    DROP TABLE [Products];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915123210_AddFollowUpAndInteractionFields_Master'
)
BEGIN
    CREATE TABLE [FollowUps] (
        [FollowUpId] int NOT NULL IDENTITY,
        [CustomerId] int NULL,
        [RepairRequestId] int NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Notes] nvarchar(2000) NOT NULL,
        [ScheduledAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Channel] int NOT NULL,
        [Status] int NOT NULL,
        [AssignedToUserId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_FollowUps] PRIMARY KEY ([FollowUpId]),
        CONSTRAINT [FK_FollowUps_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FollowUps_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915123210_AddFollowUpAndInteractionFields_Master'
)
BEGIN
    CREATE INDEX [IX_FollowUps_CustomerId] ON [FollowUps] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915123210_AddFollowUpAndInteractionFields_Master'
)
BEGIN
    CREATE INDEX [IX_FollowUps_RepairRequestId] ON [FollowUps] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915123210_AddFollowUpAndInteractionFields_Master'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260915123210_AddFollowUpAndInteractionFields_Master', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [Payments] ADD [IsVoid] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [MaxInactiveDays] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [MaxRedemptionsPerCustomer] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [MinTotalSpent] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [MinTransactions] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [MinVisitsPerPeriod] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [PointsValidityDays] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [RedeemPointsRequired] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [RewardType] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [RewardValue] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    ALTER TABLE [LoyaltyPrograms] ADD [VisitPeriodDays] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233611_AddIsVoidToMasterPayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923233611_AddIsVoidToMasterPayments', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [Description] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [Duration] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [DurationMonths] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [EnableMultiBranching] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [IsArchived] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Subscriptions] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD [Address] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD [City] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD [ContactEmail] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD [ContactPhone] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD [Country] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD [PostalCode] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD [StateOrProvince] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD [SubscriptionId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    CREATE INDEX [IX_RepairRequests_RequestDate] ON [RepairRequests] ([RequestDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    CREATE INDEX [IX_RepairRequests_Status] ON [RepairRequests] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    CREATE INDEX [IX_Payments_PaymentDate] ON [Payments] ([PaymentDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    CREATE INDEX [IX_FollowUps_Status] ON [FollowUps] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    CREATE INDEX [IX_Customers_Email] ON [Customers] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    CREATE INDEX [IX_Customers_LastName] ON [Customers] ([LastName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    CREATE INDEX [IX_Customers_Phone] ON [Customers] ([Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    CREATE INDEX [IX_Companies_SubscriptionId] ON [Companies] ([SubscriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    ALTER TABLE [Companies] ADD CONSTRAINT [FK_Companies_Subscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [Subscriptions] ([SubscriptionId]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929115042_AddTenantAndSubscriptionManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929115042_AddTenantAndSubscriptionManagement', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Supplier] ADD [City] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Supplier] ADD [ContactFirstName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Supplier] ADD [ContactLastName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Supplier] ADD [Country] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Supplier] ADD [PostalCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Supplier] ADD [StateOrProvince] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Devices] ADD [CustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Customers] ADD [City] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Customers] ADD [Country] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Customers] ADD [PostalCode] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Customers] ADD [StateOrProvince] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Companies] ADD [ContactFirstName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Companies] ADD [ContactLastName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    CREATE INDEX [IX_Devices_CustomerId] ON [Devices] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    CREATE INDEX [IX_Customers_City] ON [Customers] ([City]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    ALTER TABLE [Devices] ADD CONSTRAINT [FK_Devices_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123457_Normalize3NFMappings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929123457_Normalize3NFMappings', N'10.0.11');
END;

COMMIT;
GO



-- TENANT & REPAIR CRM SCHEMA
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE TABLE [Customers] (
        [CustomerId] int NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [Phone] nvarchar(50) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [LoyaltyPoints] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE TABLE [Devices] (
        [DeviceId] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [DeviceCode] nvarchar(50) NOT NULL,
        [DeviceName] nvarchar(200) NOT NULL,
        [DeviceType] nvarchar(100) NULL,
        [Brand] nvarchar(100) NULL,
        [Model] nvarchar(100) NULL,
        [SerialNumber] nvarchar(100) NULL,
        [PurchasePrice] decimal(18,2) NULL,
        [PurchaseDate] datetime2 NULL,
        [WarrantyStatus] nvarchar(50) NULL,
        [WarrantyExpiry] datetime2 NULL,
        [Status] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([DeviceId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE TABLE [RepairRequests] (
        [RepairRequestId] int NOT NULL IDENTITY,
        [RequestNumber] nvarchar(50) NOT NULL,
        [CustomerId] int NOT NULL,
        [DeviceId] int NULL,
        [DeviceModel] nvarchar(200) NOT NULL,
        [SerialNumber] nvarchar(100) NOT NULL,
        [IssueDescription] nvarchar(2000) NOT NULL,
        [Status] int NOT NULL,
        [Priority] int NOT NULL,
        [RequestDate] datetime2 NOT NULL,
        [CompletionDate] datetime2 NULL,
        [EstimatedCost] decimal(18,2) NULL,
        [ActualCost] decimal(18,2) NULL,
        [PartsCost] decimal(18,2) NULL,
        [LaborCost] decimal(18,2) NULL,
        [TechnicianNotes] nvarchar(2000) NULL,
        [AssignedToStaffId] nvarchar(max) NULL,
        [AssignedToManagerId] nvarchar(max) NULL,
        CONSTRAINT [PK_RepairRequests] PRIMARY KEY ([RepairRequestId]),
        CONSTRAINT [FK_RepairRequests_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE CASCADE,
        CONSTRAINT [FK_RepairRequests_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([DeviceId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE TABLE [CustomerInteractions] (
        [CustomerInteractionId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [RepairRequestId] int NULL,
        [InteractionType] int NOT NULL,
        [Notes] nvarchar(1000) NOT NULL,
        [InteractionDate] datetime2 NOT NULL,
        [InteractionByUserId] nvarchar(max) NULL,
        CONSTRAINT [PK_CustomerInteractions] PRIMARY KEY ([CustomerInteractionId]),
        CONSTRAINT [FK_CustomerInteractions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE CASCADE,
        CONSTRAINT [FK_CustomerInteractions_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE TABLE [Payments] (
        [PaymentId] int NOT NULL IDENTITY,
        [RepairRequestId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [PaymentMethod] nvarchar(50) NULL,
        [ReferenceNumber] nvarchar(100) NULL,
        [IsPaid] bit NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [FK_Payments_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE TABLE [RepairStatusHistories] (
        [RepairStatusHistoryId] int NOT NULL IDENTITY,
        [RepairRequestId] int NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [ChangedByUserId] nvarchar(max) NULL,
        [Notes] nvarchar(500) NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RepairStatusHistories] PRIMARY KEY ([RepairStatusHistoryId]),
        CONSTRAINT [FK_RepairStatusHistories_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE INDEX [IX_CustomerInteractions_CustomerId] ON [CustomerInteractions] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE INDEX [IX_CustomerInteractions_RepairRequestId] ON [CustomerInteractions] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE INDEX [IX_Payments_RepairRequestId] ON [Payments] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE INDEX [IX_RepairRequests_CustomerId] ON [RepairRequests] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE INDEX [IX_RepairRequests_DeviceId] ON [RepairRequests] ([DeviceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RepairRequests_RequestNumber] ON [RepairRequests] ([RequestNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    CREATE INDEX [IX_RepairStatusHistories_RepairRequestId] ON [RepairStatusHistories] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913103257_InitialTenantCrm'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913103257_InitialTenantCrm', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913113324_AddPartsSuppliersToTenantCrm'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [SupplierId] int NOT NULL IDENTITY,
        [SupplierCode] nvarchar(50) NOT NULL,
        [SupplierName] nvarchar(200) NOT NULL,
        [ContactPerson] nvarchar(100) NULL,
        [ContactNumber] nvarchar(50) NULL,
        [EmailAddress] nvarchar(200) NULL,
        [Address] nvarchar(500) NULL,
        [Notes] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([SupplierId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913113324_AddPartsSuppliersToTenantCrm'
)
BEGIN
    CREATE TABLE [Parts] (
        [PartId] int NOT NULL IDENTITY,
        [PartCode] nvarchar(50) NOT NULL,
        [PartName] nvarchar(200) NOT NULL,
        [Category] nvarchar(100) NULL,
        [Manufacturer] nvarchar(100) NULL,
        [Model] nvarchar(100) NULL,
        [UnitCost] decimal(18,2) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [QuantityOnHand] int NOT NULL,
        [ReorderLevel] int NOT NULL,
        [SupplierId] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Parts] PRIMARY KEY ([PartId]),
        CONSTRAINT [FK_Parts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([SupplierId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913113324_AddPartsSuppliersToTenantCrm'
)
BEGIN
    CREATE TABLE [RepairParts] (
        [RepairPartId] int NOT NULL IDENTITY,
        [RepairRequestId] int NOT NULL,
        [PartId] int NOT NULL,
        [QuantityUsed] int NOT NULL,
        [UnitCostAtTime] decimal(18,2) NOT NULL,
        [UnitPriceAtTime] decimal(18,2) NOT NULL,
        [UsedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RepairParts] PRIMARY KEY ([RepairPartId]),
        CONSTRAINT [FK_RepairParts_Parts_PartId] FOREIGN KEY ([PartId]) REFERENCES [Parts] ([PartId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RepairParts_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913113324_AddPartsSuppliersToTenantCrm'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Parts_PartCode] ON [Parts] ([PartCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913113324_AddPartsSuppliersToTenantCrm'
)
BEGIN
    CREATE INDEX [IX_Parts_SupplierId] ON [Parts] ([SupplierId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913113324_AddPartsSuppliersToTenantCrm'
)
BEGIN
    CREATE INDEX [IX_RepairParts_PartId] ON [RepairParts] ([PartId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913113324_AddPartsSuppliersToTenantCrm'
)
BEGIN
    CREATE INDEX [IX_RepairParts_RepairRequestId] ON [RepairParts] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913113324_AddPartsSuppliersToTenantCrm'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Suppliers_SupplierCode] ON [Suppliers] ([SupplierCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913113324_AddPartsSuppliersToTenantCrm'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913113324_AddPartsSuppliersToTenantCrm', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] DROP CONSTRAINT [FK_CustomerInteractions_Customers_CustomerId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] DROP CONSTRAINT [FK_CustomerInteractions_RepairRequests_RepairRequestId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerInteractions]') AND [c].[name] = N'Notes');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [CustomerInteractions] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [CustomerInteractions] ALTER COLUMN [Notes] nvarchar(2000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerInteractions]') AND [c].[name] = N'InteractionByUserId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [CustomerInteractions] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [CustomerInteractions] ALTER COLUMN [InteractionByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerInteractions]') AND [c].[name] = N'CustomerId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [CustomerInteractions] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [CustomerInteractions] ALTER COLUMN [CustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [ClosedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [Priority] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [Resolution] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [Status] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [Subject] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD CONSTRAINT [FK_CustomerInteractions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    ALTER TABLE [CustomerInteractions] ADD CONSTRAINT [FK_CustomerInteractions_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915115319_AddInteractionCrudFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260915115319_AddInteractionCrudFields', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915123148_AddFollowUpAndInteractionFields'
)
BEGIN
    CREATE TABLE [FollowUps] (
        [FollowUpId] int NOT NULL IDENTITY,
        [CustomerId] int NULL,
        [RepairRequestId] int NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Notes] nvarchar(2000) NOT NULL,
        [ScheduledAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [Channel] int NOT NULL,
        [Status] int NOT NULL,
        [AssignedToUserId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_FollowUps] PRIMARY KEY ([FollowUpId]),
        CONSTRAINT [FK_FollowUps_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FollowUps_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915123148_AddFollowUpAndInteractionFields'
)
BEGIN
    CREATE INDEX [IX_FollowUps_CustomerId] ON [FollowUps] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915123148_AddFollowUpAndInteractionFields'
)
BEGIN
    CREATE INDEX [IX_FollowUps_RepairRequestId] ON [FollowUps] ([RepairRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260915123148_AddFollowUpAndInteractionFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260915123148_AddFollowUpAndInteractionFields', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233453_AddIsVoidToPayments'
)
BEGIN
    ALTER TABLE [Payments] ADD [IsVoid] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260923233453_AddIsVoidToPayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260923233453_AddIsVoidToPayments', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928014643_AddRetentionAndEmailCampaigns'
)
BEGIN
    CREATE TABLE [RetentionEmailTemplates] (
        [RetentionEmailTemplateId] int NOT NULL IDENTITY,
        [Segment] int NOT NULL,
        [TemplateName] nvarchar(150) NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [DefaultDiscountPercent] decimal(5,2) NOT NULL,
        [ValidityDays] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_RetentionEmailTemplates] PRIMARY KEY ([RetentionEmailTemplateId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928014643_AddRetentionAndEmailCampaigns'
)
BEGIN
    CREATE TABLE [RetentionRequests] (
        [RetentionRequestId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [TargetSegment] int NOT NULL,
        [ActionType] nvarchar(100) NOT NULL,
        [ProposedDiscountPercent] decimal(5,2) NOT NULL,
        [RetentionDetails] nvarchar(2000) NOT NULL,
        [ReasonCategory] nvarchar(150) NOT NULL,
        [ReasonNote] nvarchar(2000) NULL,
        [Status] int NOT NULL,
        [SubmittedByUserId] nvarchar(450) NOT NULL,
        [SubmittedByName] nvarchar(200) NOT NULL,
        [SubmittedAt] datetime2 NOT NULL,
        [ReviewedByUserId] nvarchar(450) NULL,
        [ReviewedByName] nvarchar(200) NULL,
        [ReviewedAt] datetime2 NULL,
        [ReviewRemarks] nvarchar(2000) NULL,
        [RejectionReason] nvarchar(2000) NULL,
        [AddedToCampaign] bit NOT NULL,
        [CampaignAddedAt] datetime2 NULL,
        CONSTRAINT [PK_RetentionRequests] PRIMARY KEY ([RetentionRequestId]),
        CONSTRAINT [FK_RetentionRequests_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928014643_AddRetentionAndEmailCampaigns'
)
BEGIN
    CREATE TABLE [RetentionSettings] (
        [RetentionSettingsId] int NOT NULL IDENTITY,
        [InactiveThresholdDays] int NOT NULL,
        [AtRiskThresholdDays] int NOT NULL,
        [AntiFatigueDays] int NOT NULL,
        [DefaultOfferValidityDays] int NOT NULL,
        [SmtpHost] nvarchar(200) NULL,
        [SmtpPort] int NOT NULL,
        [SmtpUsername] nvarchar(200) NULL,
        [SmtpPassword] nvarchar(200) NULL,
        [SmtpFromEmail] nvarchar(200) NULL,
        [SmtpFromName] nvarchar(200) NULL,
        [SmtpEnableSsl] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RetentionSettings] PRIMARY KEY ([RetentionSettingsId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928014643_AddRetentionAndEmailCampaigns'
)
BEGIN
    CREATE TABLE [RetentionEmailLogs] (
        [RetentionEmailLogId] int NOT NULL IDENTITY,
        [RetentionRequestId] int NULL,
        [CustomerId] int NOT NULL,
        [RecipientEmail] nvarchar(200) NOT NULL,
        [RecipientName] nvarchar(200) NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [FormattedBody] nvarchar(max) NOT NULL,
        [Segment] int NOT NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [PromoCode] nvarchar(50) NULL,
        [ValidUntil] datetime2 NULL,
        [IsDispatched] bit NOT NULL,
        [DispatchedAt] datetime2 NULL,
        [DispatchedByUserId] nvarchar(450) NULL,
        [IsAutomated] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [DeliveryStatus] nvarchar(50) NOT NULL,
        [DeliveryError] nvarchar(2000) NULL,
        CONSTRAINT [PK_RetentionEmailLogs] PRIMARY KEY ([RetentionEmailLogId]),
        CONSTRAINT [FK_RetentionEmailLogs_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RetentionEmailLogs_RetentionRequests_RetentionRequestId] FOREIGN KEY ([RetentionRequestId]) REFERENCES [RetentionRequests] ([RetentionRequestId]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928014643_AddRetentionAndEmailCampaigns'
)
BEGIN
    CREATE INDEX [IX_RetentionEmailLogs_CustomerId] ON [RetentionEmailLogs] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928014643_AddRetentionAndEmailCampaigns'
)
BEGIN
    CREATE INDEX [IX_RetentionEmailLogs_RetentionRequestId] ON [RetentionEmailLogs] ([RetentionRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928014643_AddRetentionAndEmailCampaigns'
)
BEGIN
    CREATE INDEX [IX_RetentionRequests_CustomerId] ON [RetentionRequests] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928014643_AddRetentionAndEmailCampaigns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928014643_AddRetentionAndEmailCampaigns', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Payments] DROP CONSTRAINT [FK_Payments_RepairRequests_RepairRequestId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RepairRequests] DROP CONSTRAINT [FK_RepairRequests_Customers_CustomerId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RepairRequests] DROP CONSTRAINT [FK_RepairRequests_Devices_DeviceId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RetentionRequests]') AND [c].[name] = N'ReviewedByName');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [RetentionRequests] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [RetentionRequests] DROP COLUMN [ReviewedByName];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RetentionRequests]') AND [c].[name] = N'SubmittedByName');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [RetentionRequests] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [RetentionRequests] DROP COLUMN [SubmittedByName];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RetentionEmailLogs]') AND [c].[name] = N'RecipientName');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [RetentionEmailLogs] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [RetentionEmailLogs] DROP COLUMN [RecipientName];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Suppliers]') AND [c].[name] = N'ContactPerson');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Suppliers] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [Suppliers] DROP COLUMN [ContactPerson];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [StateOrProvince] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [City] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [ContactFirstName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [ContactLastName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [Country] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [PostalCode] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RetentionRequests] ADD [ReviewedByFirstName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RetentionRequests] ADD [ReviewedByLastName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RetentionRequests] ADD [SubmittedByFirstName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RetentionRequests] ADD [SubmittedByLastName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RetentionEmailLogs] ADD [RecipientFirstName] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RetentionEmailLogs] ADD [RecipientLastName] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Devices] ADD [CustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Customers] ADD [City] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Customers] ADD [Country] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Customers] ADD [PostalCode] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Customers] ADD [StateOrProvince] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_RetentionRequests_Status] ON [RetentionRequests] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_RetentionEmailLogs_CreatedAt] ON [RetentionEmailLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_RepairRequests_RequestDate] ON [RepairRequests] ([RequestDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_RepairRequests_Status] ON [RepairRequests] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_Payments_PaymentDate] ON [Payments] ([PaymentDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_FollowUps_Status] ON [FollowUps] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_Devices_CustomerId] ON [Devices] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_Customers_City] ON [Customers] ([City]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_Customers_Email] ON [Customers] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_Customers_LastName] ON [Customers] ([LastName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    CREATE INDEX [IX_Customers_Phone] ON [Customers] ([Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Devices] ADD CONSTRAINT [FK_Devices_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [Payments] ADD CONSTRAINT [FK_Payments_RepairRequests_RepairRequestId] FOREIGN KEY ([RepairRequestId]) REFERENCES [RepairRequests] ([RepairRequestId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RepairRequests] ADD CONSTRAINT [FK_RepairRequests_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    ALTER TABLE [RepairRequests] ADD CONSTRAINT [FK_RepairRequests_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([DeviceId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123535_Normalize3NFTenantMappings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929123535_Normalize3NFTenantMappings', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930063930_AddSyncQueueTable'
)
BEGIN
    CREATE TABLE [SyncQueue] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(100) NOT NULL,
        [Operation] nvarchar(50) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [RetryCount] int NOT NULL,
        [LastError] nvarchar(2000) NULL,
        [SyncedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_SyncQueue] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930063930_AddSyncQueueTable'
)
BEGIN
    CREATE INDEX [IX_SyncQueue_CreatedAtUtc] ON [SyncQueue] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930063930_AddSyncQueueTable'
)
BEGIN
    CREATE INDEX [IX_SyncQueue_Status] ON [SyncQueue] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930063930_AddSyncQueueTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930063930_AddSyncQueueTable', N'10.0.11');
END;

COMMIT;
GO


