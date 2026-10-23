using Shouldly;
using Grinder.Api.Models.Entities;
using Grinder.Api.Utilities;

namespace Grinder.Api.Tests.Unit;

public class ValidatorTest
{
    [Theory]
    [InlineData("", "name", "hashedpassword", "NZ", false)]
    [InlineData("example@email.com", "", "hashedpassword", "NZ", false)]
    [InlineData("example@email.com", "name", "", "NZ", false)]
    [InlineData("example@email.com", "name", "hashedpassword", "", false)]
    [InlineData("example@email.com", "name", "hashedpassword", "NZ", true)]
    public void Required_Fields(string email, string displayName, string hashedPassword, string country, bool expected)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = hashedPassword,
            Country = country
        };
        ValidationHelper.ValidateEntity(user).ShouldBe(expected);
    }

    [Theory]
    [InlineData("jane@doe.nz", true)]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    [InlineData("invalid@email", false)]
    [InlineData("valid@email.n", true)]
    [InlineData("invalid@email.n!", false)]
    [InlineData("in..valid@email.nz", false)]
    [InlineData(".invalid@email.nz", false)]
    [InlineData("valid@email.ac.uc.nz", true)]
    [InlineData("invalid@email.ac.uc.11", false)]
    [InlineData("valid@email.ac.uc.1z", true)]
    [InlineData("valid!@email.ac.uc.nz", true)]
    [InlineData("Ngāruawāhia@Ngāpuhi.Tūhoe", false)]
    public void EmailValidation_VariousInputs_ReturnExpected(string email, bool expected)
    {
        ValidationHelper.ValidateEmail(email).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Password1!", true)]
    [InlineData("P4$$word", true)]
    [InlineData("P4$$ word", true)]
    [InlineData("lowercase", false)]
    [InlineData("UPPERCASE", false)]
    [InlineData("lowerUPPER", false)]
    [InlineData("lower1", false)]
    [InlineData("UPPER1", false)]
    [InlineData("lowerUPPER1", false)]
    [InlineData("lower!", false)]
    [InlineData("UPPER!", false)]
    [InlineData("lowerUPPER!", false)]
    [InlineData("12345678!", false)]
    [InlineData("pass", false)]
    [InlineData("P4$$", false)]
    public void PasswordValidation_VariousInputs_ReturnExpected(string password, bool expected)
    {
        ValidationHelper.ValidatePassword(password).ShouldBe(expected);
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/svg+xml", true)]
    [InlineData("image/gif", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/pdf", false)]
    [InlineData("image/pdf", false)]
    [InlineData("image/webp", false)]
    public void ImageContentTypeValidation_VariousInputs_ReturnExpected(string fileType, bool expected)
    {
        ValidationHelper.ValidateImageContentType(fileType).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4 * 1024 * 1024, true)]
    [InlineData(5 * 1024 * 1024, true)]
    [InlineData(5 * 1024 * 1024 + 1, false)]
    [InlineData(10 * 1024 * 1024, false)]
    public void ImageSizeValidation_VariousInputs_ReturnExpected(long fileSize, bool expected)
    {
        ValidationHelper.ValidateImageSize(fileSize).ShouldBe(expected);
    }
}

