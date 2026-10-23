using Grinder.Api.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.DTOs;

public record GenerateVerificationCodeRequest(CodeType Type, string Email);
