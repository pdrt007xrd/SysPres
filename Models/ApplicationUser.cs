using System.ComponentModel.DataAnnotations;

namespace SysPres.Models;

public class ApplicationUser
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? FullName { get; set; }

    [Required]
    [MaxLength(100)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Role { get; set; }

    [MaxLength(500)]
    public string? Permissions { get; set; }

    public bool IsActive { get; set; } = true;
}
