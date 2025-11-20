/**
 * File: InsertPostCommentSampleData.sql
 * Created: 2025-01-15
 * Purpose: Insert sample PostComment data for testing
 *          PostId: 37E70E46-C3E4-41E6-B68E-0EFCDECDA357
 *          UserId 1: B0C48DE2-3273-493F-87B6-557E35AC9EE4
 *          UserId 2: A44308EB-A40B-47F5-AE2F-3CDF4F29402B
 */

USE [KeytietkiemDB]
GO

SET NOCOUNT ON;
GO

BEGIN TRANSACTION;
GO

-- Declare variables for PostId and UserIds
DECLARE @PostId UNIQUEIDENTIFIER = '37E70E46-C3E4-41E6-B68E-0EFCDECDA357';
DECLARE @UserId1 UNIQUEIDENTIFIER = 'B0C48DE2-3273-493F-87B6-557E35AC9EE4';
DECLARE @UserId2 UNIQUEIDENTIFIER = 'A44308EB-A40B-47F5-AE2F-3CDF4F29402B';

-- Variables to store generated CommentIds for replies
DECLARE @Comment1 UNIQUEIDENTIFIER;
DECLARE @Comment2 UNIQUEIDENTIFIER;
DECLARE @Comment3 UNIQUEIDENTIFIER;
DECLARE @Comment4 UNIQUEIDENTIFIER;
DECLARE @Comment5 UNIQUEIDENTIFIER;
DECLARE @ReplyToComment3 UNIQUEIDENTIFIER;

-- Validate Post exists
IF NOT EXISTS (SELECT 1 FROM [dbo].[Posts] WHERE [PostID] = @PostId)
BEGIN
    DECLARE @PostIdStr NVARCHAR(36) = CAST(@PostId AS NVARCHAR(36));
    RAISERROR('Post với ID %s không tồn tại', 16, 1, @PostIdStr);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Validate Users exist
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [UserId] = @UserId1)
BEGIN
    DECLARE @UserId1Str NVARCHAR(36) = CAST(@UserId1 AS NVARCHAR(36));
    RAISERROR('User với ID %s không tồn tại', 16, 1, @UserId1Str);
    ROLLBACK TRANSACTION;
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [UserId] = @UserId2)
BEGIN
    DECLARE @UserId2Str NVARCHAR(36) = CAST(@UserId2 AS NVARCHAR(36));
    RAISERROR('User với ID %s không tồn tại', 16, 1, @UserId2Str);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Insert top-level comments (ParentCommentId = NULL)
-- Comment 1
SET @Comment1 = NEWID();
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (@Comment1, @PostId, @UserId1, NULL, N'Bài viết rất hay và hữu ích! Cảm ơn tác giả đã chia sẻ.', DATEADD(DAY, -5, GETDATE()), 1);

-- Comment 2
SET @Comment2 = NEWID();
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (@Comment2, @PostId, @UserId2, NULL, N'Tôi đã thử và thấy rất hiệu quả. Mong tác giả viết thêm các bài tương tự.', DATEADD(DAY, -4, GETDATE()), 1);

-- Comment 3
SET @Comment3 = NEWID();
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (@Comment3, @PostId, @UserId1, NULL, N'Có một số điểm tôi chưa hiểu rõ, có thể giải thích thêm không?', DATEADD(DAY, -3, GETDATE()), 0);

-- Comment 4
SET @Comment4 = NEWID();
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (@Comment4, @PostId, @UserId2, NULL, N'Bài viết này đã giúp tôi giải quyết được vấn đề đang gặp phải. Cảm ơn rất nhiều!', DATEADD(DAY, -2, GETDATE()), 1);

-- Comment 5
SET @Comment5 = NEWID();
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (@Comment5, @PostId, @UserId1, NULL, N'Tôi muốn hỏi về phần thứ 3, có thể mở rộng thêm không?', DATEADD(DAY, -1, GETDATE()), 0);

-- Insert nested replies (ParentCommentId pointing to existing comments)
-- Reply to Comment 1
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (NEWID(), @PostId, @UserId2, @Comment1, N'Đồng ý với bạn! Bài viết này thực sự rất chi tiết và dễ hiểu.', DATEADD(DAY, -4, GETDATE()), 1);

-- Reply to Comment 2
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (NEWID(), @PostId, @UserId1, @Comment2, N'Cảm ơn bạn đã phản hồi! Tôi sẽ cố gắng viết thêm các bài tương tự.', DATEADD(DAY, -3, GETDATE()), 1);

-- Reply to Comment 3 (nested reply - reply to reply)
SET @ReplyToComment3 = NEWID();
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (@ReplyToComment3, @PostId, @UserId2, @Comment3, N'Bạn có thể nói rõ hơn về điểm nào bạn chưa hiểu không?', DATEADD(DAY, -2, GETDATE()), 1);

-- Reply to the reply of Comment 3 (nested reply level 2)
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (NEWID(), @PostId, @UserId1, @ReplyToComment3, N'Cụ thể là phần về cách xử lý lỗi, tôi muốn hiểu sâu hơn.', DATEADD(DAY, -1, GETDATE()), 0);

-- Reply to Comment 4
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (NEWID(), @PostId, @UserId1, @Comment4, N'Rất vui khi bài viết hữu ích với bạn!', GETDATE(), 1);

-- Reply to Comment 5
INSERT INTO [dbo].[PostComments] ([CommentId], [PostId], [UserId], [ParentCommentId], [Content], [CreatedAt], [IsApproved])
VALUES (NEWID(), @PostId, @UserId2, @Comment5, N'Tôi sẽ cập nhật thêm thông tin về phần đó trong bài viết tiếp theo.', GETDATE(), 0);
GO

COMMIT TRANSACTION;
GO

SET NOCOUNT OFF;
GO

PRINT 'Đã chèn thành công dữ liệu mẫu cho PostComments';
GO

