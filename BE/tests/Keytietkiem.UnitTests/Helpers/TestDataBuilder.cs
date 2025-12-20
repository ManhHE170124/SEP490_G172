using System;
using Keytietkiem.Models;

namespace Keytietkiem.UnitTests.Helpers
{
    public static class TestDataBuilder
    {
        public static User CreateUser(
            Guid? userId = null,
            string? firstName = "Test",
            string? lastName = "User",
            string? email = "test@example.com",
            string status = "Active")
        {
            return new User
            {
                UserId = userId ?? Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                FullName = $"{firstName} {lastName}",
                Email = email ?? $"test{Guid.NewGuid()}@example.com",
                Status = status,
                CreatedAt = DateTime.Now
            };
        }

        public static PostType CreatePostType(
            Guid? postTypeId = null,
            string? postTypeName = "Test Post Type",
            string? description = null)
        {
            return new PostType
            {
                PostTypeId = postTypeId ?? Guid.NewGuid(),
                PostTypeName = postTypeName ?? "Test Post Type",
                Description = description,
                CreatedAt = DateTime.Now
            };
        }

        public static Tag CreateTag(
            Guid? tagId = null,
            string? tagName = "Test Tag",
            string? slug = "test-tag")
        {
            return new Tag
            {
                TagId = tagId ?? Guid.NewGuid(),
                TagName = tagName ?? "Test Tag",
                Slug = slug ?? "test-tag",
                CreatedAt = DateTime.Now
            };
        }

        public static Post CreatePost(
            Guid? postId = null,
            string? title = "Test Post",
            string? slug = "test-post",
            Guid? authorId = null,
            Guid? postTypeId = null,
            string status = "Draft")
        {
            return new Post
            {
                PostId = postId ?? Guid.NewGuid(),
                Title = title ?? "Test Post",
                Slug = slug ?? "test-post",
                AuthorId = authorId,
                PostTypeId = postTypeId,
                Status = status,
                ViewCount = 0,
                CreatedAt = DateTime.Now
            };
        }

        public static Module CreateModule(
            long? moduleId = null,
            string? moduleName = "Test Module",
            string? code = "TEST_MODULE",
            string? description = null)
        {
            return new Module
            {
                ModuleId = moduleId ?? 1,
                ModuleName = moduleName ?? "Test Module",
                Code = code ?? "TEST_MODULE",
                Description = description,
                CreatedAt = DateTime.Now
            };
        }

        public static Permission CreatePermission(
            long? permissionId = null,
            string? permissionName = "Test Permission",
            string? code = "TEST_PERMISSION",
            string? description = null)
        {
            return new Permission
            {
                PermissionId = permissionId ?? 1,
                PermissionName = permissionName ?? "Test Permission",
                Code = code ?? "TEST_PERMISSION",
                Description = description,
                CreatedAt = DateTime.Now
            };
        }

        public static Role CreateRole(
            string? roleId = null,
            string? name = "Test Role",
            string? code = "TEST_ROLE",
            bool isSystem = false,
            bool isActive = true)
        {
            return new Role
            {
                RoleId = roleId ?? "TEST_ROLE",
                Name = name ?? "Test Role",
                Code = code ?? "TEST_ROLE",
                IsSystem = isSystem,
                IsActive = isActive,
                CreatedAt = DateTime.Now
            };
        }

        public static RolePermission CreateRolePermission(
            string roleId,
            long moduleId,
            long permissionId,
            bool isActive = true)
        {
            return new RolePermission
            {
                RoleId = roleId,
                ModuleId = moduleId,
                PermissionId = permissionId,
                IsActive = isActive
            };
        }

        public static PostComment CreatePostComment(
            Guid? commentId = null,
            Guid? postId = null,
            Guid? userId = null,
            Guid? parentCommentId = null,
            string? content = "Test comment",
            bool? isApproved = true)
        {
            return new PostComment
            {
                CommentId = commentId ?? Guid.NewGuid(),
                PostId = postId ?? Guid.NewGuid(),
                UserId = userId ?? Guid.NewGuid(),
                ParentCommentId = parentCommentId,
                Content = content ?? "Test comment",
                IsApproved = isApproved,
                CreatedAt = DateTime.Now
            };
        }
    }
}

