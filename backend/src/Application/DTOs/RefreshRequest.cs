using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

public record RefreshRequest([Required] string RefreshToken);
