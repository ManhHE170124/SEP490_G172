  use KeytietkiemDB;

  ALTER TABLE [dbo].[Users]
    DROP CONSTRAINT [CK__Users__Status__318258D2];
    GO

  
  ALTER TABLE [dbo].[Users]
ADD CONSTRAINT [CK__Users__Status__318258D2]
CHECK ([Status] IN ('Active', 'Locked', 'Disabled', 'Temp'));
GO