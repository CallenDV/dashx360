namespace XboxMetroLauncher.Models;

public sealed class DashboardTileCustomization
{
	public string ImagePath { get; set; } = string.Empty;

	public string TitleOverride { get; set; } = string.Empty;

	public double Zoom { get; set; } = 1.0;

	public double OffsetX { get; set; }

	public double OffsetY { get; set; }
}
