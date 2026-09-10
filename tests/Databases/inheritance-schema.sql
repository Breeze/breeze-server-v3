-- Inheritance model tables for BreezeTestDb.
-- Generated from the EF model via InheritanceContext.Database.GenerateCreateScript().
-- Regenerate this file if Model_Inheritance.EFCore changes.

CREATE TABLE [AccountTypes] (
    [Id] int NOT NULL,
    [Name] nvarchar(max) NULL,
    CONSTRAINT [PK_AccountTypes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [BankAccountTPCs] (
    [Id] int NOT NULL,
    [BankName] nvarchar(max) NULL,
    [Swift] nvarchar(max) NULL,
    [AccountTypeId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Owner] nvarchar(max) NULL,
    [Number] nvarchar(max) NULL,
    [InheritanceModel] nvarchar(max) NULL,
    CONSTRAINT [PK_BankAccountTPCs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BankAccountTPCs_AccountTypes_AccountTypeId] FOREIGN KEY ([AccountTypeId]) REFERENCES [AccountTypes] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [BillingDetailTPHs] (
    [Id] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Owner] nvarchar(max) NULL,
    [Number] nvarchar(max) NULL,
    [AccountTypeId] int NOT NULL,
    [InheritanceModel] nvarchar(max) NULL,
    [BillingDetailType] nvarchar(21) NOT NULL,
    [BankName] nvarchar(max) NULL,
    [Swift] nvarchar(max) NULL,
    [ExpiryMonth] nvarchar(max) NULL,
    [ExpiryYear] nvarchar(max) NULL,
    CONSTRAINT [PK_BillingDetailTPHs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BillingDetailTPHs_AccountTypes_AccountTypeId] FOREIGN KEY ([AccountTypeId]) REFERENCES [AccountTypes] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [BillingDetailTPTs] (
    [Id] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Owner] nvarchar(max) NULL,
    [Number] nvarchar(max) NULL,
    [AccountTypeId] int NOT NULL,
    [InheritanceModel] nvarchar(max) NULL,
    CONSTRAINT [PK_BillingDetailTPTs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BillingDetailTPTs_AccountTypes_AccountTypeId] FOREIGN KEY ([AccountTypeId]) REFERENCES [AccountTypes] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [CreditCardsTPCs] (
    [Id] int NOT NULL,
    [AccountTypeId] int NOT NULL,
    [ExpiryMonth] nvarchar(max) NULL,
    [ExpiryYear] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Owner] nvarchar(max) NULL,
    [Number] nvarchar(max) NULL,
    [InheritanceModel] nvarchar(max) NULL,
    CONSTRAINT [PK_CreditCardsTPCs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CreditCardsTPCs_AccountTypes_AccountTypeId] FOREIGN KEY ([AccountTypeId]) REFERENCES [AccountTypes] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [DepositTPCs] (
    [Id] int NOT NULL IDENTITY,
    [BankAccountId] int NOT NULL,
    [Amount] real NOT NULL,
    [Deposited] datetime2 NOT NULL,
    CONSTRAINT [PK_DepositTPCs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DepositTPCs_BankAccountTPCs_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [BankAccountTPCs] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [DepositTPHs] (
    [Id] int NOT NULL IDENTITY,
    [BankAccountId] int NOT NULL,
    [Amount] real NOT NULL,
    [Deposited] datetime2 NOT NULL,
    CONSTRAINT [PK_DepositTPHs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DepositTPHs_BillingDetailTPHs_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [BillingDetailTPHs] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [BankAccountTPTs] (
    [Id] int NOT NULL,
    [BankName] nvarchar(max) NULL,
    [Swift] nvarchar(max) NULL,
    CONSTRAINT [PK_BankAccountTPTs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BankAccountTPTs_BillingDetailTPTs_Id] FOREIGN KEY ([Id]) REFERENCES [BillingDetailTPTs] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [CreditCardTPTs] (
    [Id] int NOT NULL,
    [ExpiryMonth] nvarchar(max) NULL,
    [ExpiryYear] nvarchar(max) NULL,
    CONSTRAINT [PK_CreditCardTPTs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CreditCardTPTs_BillingDetailTPTs_Id] FOREIGN KEY ([Id]) REFERENCES [BillingDetailTPTs] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [DepositTPTs] (
    [Id] int NOT NULL IDENTITY,
    [BankAccountId] int NOT NULL,
    [Amount] real NOT NULL,
    [Deposited] datetime2 NOT NULL,
    CONSTRAINT [PK_DepositTPTs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DepositTPTs_BankAccountTPTs_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [BankAccountTPTs] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_BankAccountTPCs_AccountTypeId] ON [BankAccountTPCs] ([AccountTypeId]);
GO


CREATE INDEX [IX_BillingDetailTPHs_AccountTypeId] ON [BillingDetailTPHs] ([AccountTypeId]);
GO


CREATE INDEX [IX_BillingDetailTPTs_AccountTypeId] ON [BillingDetailTPTs] ([AccountTypeId]);
GO


CREATE INDEX [IX_CreditCardsTPCs_AccountTypeId] ON [CreditCardsTPCs] ([AccountTypeId]);
GO


CREATE INDEX [IX_DepositTPCs_BankAccountId] ON [DepositTPCs] ([BankAccountId]);
GO


CREATE INDEX [IX_DepositTPHs_BankAccountId] ON [DepositTPHs] ([BankAccountId]);
GO


CREATE INDEX [IX_DepositTPTs_BankAccountId] ON [DepositTPTs] ([BankAccountId]);
GO



