using Shouldly;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Tests.Unit;

public class CommentDeleteTest
{
    private static Comment CreateComment(string content, bool isDeleted) => new()
    {
        Id = 1,
        TaskId = 1,
        AuthorId = 1,
        Content = content,
        CreatedAt = DateTimeOffset.UtcNow,
        IsDeleted = isDeleted,
        Author = new User
        {
            Id = 1,
            Email = "test@test.com",
            DisplayName = "Test User",
            PasswordHash = string.Empty,
            Country = "NZ"
        }
    };

    [Theory]
    [InlineData("Hello world", false, "Hello world")]
    [InlineData("Hello world", true, "Deleted comment")]
    [InlineData("", true, "Deleted comment")]
    public void FromEntity_ContentReflectsDeletedState(string content, bool isDeleted, string expectedContent)
    {
        var comment = CreateComment(content, isDeleted);

        var response = CommentResponse.FromEntity(comment);

        response.Content.ShouldBe(expectedContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FromEntity_IsDeletedMatchesEntity(bool isDeleted)
    {
        var comment = CreateComment("content", isDeleted);

        var response = CommentResponse.FromEntity(comment);

        response.IsDeleted.ShouldBe(isDeleted);
    }

    [Fact]
    public void FromEntity_WhenDeleted_OnlyContentIsMasked()
    {
        var comment = CreateComment("secret text", isDeleted: true);
        comment.ParentCommentId = 42;

        var response = CommentResponse.FromEntity(comment);

        response.Content.ShouldBe("Deleted comment");
        response.ParentCommentId.ShouldBe(42);
        response.Id.ShouldBe(comment.Id);
    }
}