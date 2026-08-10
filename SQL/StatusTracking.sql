USE [Nattsafe]
GO

/****** Object:  Table [dbo].[StatusTracking]    Script Date: 8/8/2026 5:53:57 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[StatusTracking](
	[intId] [int] NOT NULL,
	[intStatus] [int] NOT NULL,
	[dtmDate] [datetime] NOT NULL
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[StatusTracking] ADD  CONSTRAINT [DF_StatusTracking_dtmDate]  DEFAULT (getdate()) FOR [dtmDate]
GO


