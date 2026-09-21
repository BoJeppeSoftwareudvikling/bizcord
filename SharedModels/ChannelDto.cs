using System.ComponentModel.DataAnnotations;

namespace SharedModels;

public class ChannelDto
{
    public int? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
