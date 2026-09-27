using System.Text.RegularExpressions;
using ZaneTask.Domain.Common;

namespace ZaneTask.Domain.Projects;

public partial class Label : Entity
{
    public const int NameMaxLength = 50;

    private Label()
    {
        Name = null!;
        Color = null!;
    }

    internal Label(Guid projectId, string name, string color)
    {
        ProjectId = projectId;
        Name = Guard.Required(name, "Label name", NameMaxLength);
        Color = NormalizeColor(color);
    }

    public Guid ProjectId { get; private set; }
    public string Name { get; private set; }

    /// <summary>Hex color such as <c>#1E88E5</c>.</summary>
    public string Color { get; private set; }

    internal void Update(string name, string color)
    {
        Name = Guard.Required(name, "Label name", NameMaxLength);
        Color = NormalizeColor(color);
    }

    private static string NormalizeColor(string? color)
    {
        if (color is null || !HexColor().IsMatch(color))
            throw new DomainException("Label color must be a hex color like #1E88E5.");
        return color.ToUpperInvariant();
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
