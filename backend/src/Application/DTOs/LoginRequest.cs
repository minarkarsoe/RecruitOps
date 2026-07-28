using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

public record LoginRequest
{
    /// <summary>Length is capped because this is anonymous, attacker-controlled input that
    /// downstream code keeps: <c>EmailAddress</c> only checks for a single '@', so without
    /// this a single request could carry megabytes. 320 is the RFC 5321 maximum
    /// (64 local + @ + 255 domain), and the stored column is varchar(256) anyway.</summary>
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Capped for the same reason. Long passphrases are fine; 512 is far past any
    /// legitimate one and stops the hasher being used as a CPU amplifier.</summary>
    [Required, StringLength(512)]
    public string Password { get; init; } = string.Empty;
}
