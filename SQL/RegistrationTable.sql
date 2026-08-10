USE [NokasMwCacheDbV3]
GO

/****** Object:  Table [dbo].[Registration]    Script Date: 8/8/2026 7:03:06 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Registration](
	[RecId] [bigint] IDENTITY(1,1) NOT NULL,
	[CustomerRecId] [int] NOT NULL,
	[BagNumber] [varchar](20) NULL,
	[ReferenceStatement] [nvarchar](50) NULL,
	[TurnOverDate] [datetime] NULL,
	[RegistrationDate] [datetime] NULL,
	[RegisteredName] [nvarchar](150) NULL,
	[RegisteredBy] [nvarchar](150) NULL,
	[TotalAmount] [float] NULL,
	[CountedAmount] [float] NULL,
	[ControlledAmount] [float] NULL,
	[ControlledDateTime] [datetime] NULL,
	[RegisteredCoins] [float] NULL,
	[RegisteredCash] [float] NULL,
	[RegisteredChecks] [float] NULL,
	[RegisteredForeignCurrency] [varchar](50) NULL,
	[PickedUpDate] [datetime] NULL,
	[DateOfSale] [datetime] NULL,
	[ShopNumber] [varchar](20) NULL,
	[SONumber] [varchar](50) NULL,
	[RegistrationStatusRecId] [int] NULL,
	[DeviationTypeId] [int] NULL,
	[LastUpdateDate] [datetime] NULL,
	[LastUpdateBy] [nvarchar](50) NULL,
	[ContainsValuta] [bit] NULL,
	[NattsafeRecordId] [int] NULL,
	[Denom50] [varchar](50) NULL,
	[Denom100] [varchar](50) NULL,
	[Denom200] [varchar](50) NULL,
	[Denom500] [varchar](50) NULL,
	[Denom1000] [varchar](50) NULL,
	[BankAccount] [varchar](50) NULL,
	[CreatedAt] [datetime] NULL,
	[CreatedBy] [nvarchar](50) NULL,
	[RegistrationType] [varchar](50) NULL,
	[CacheDBRegNumber] [varchar](50) NULL,
	[RegistrationSubType] [varchar](200) NULL,
	[Country] [nvarchar](50) NOT NULL,
	[ConfirmFlag] [nvarchar](1) NULL,
	[ConfirmDateTime] [datetime] NULL,
	[RegisteredUserId] [nvarchar](200) NULL,
	[DropNSName] [nvarchar](200) NULL,
	[DropNSDateTime] [datetime] NULL
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[Registration] ADD  DEFAULT ('Norway') FOR [Country]
GO

ALTER TABLE [dbo].[Registration] ADD  DEFAULT ('N') FOR [ConfirmFlag]
GO


