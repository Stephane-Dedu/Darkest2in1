using System;

namespace DarkestDungeon3.Runtime;

/// <summary>Completed uploads and resource callbacks, rather than elapsed loading time.</summary>
internal sealed class CampaignLoadStatus
{
    public float Progress { get; }
    public string Label { get; }
    public string Error { get; }
    public bool Ready { get; }

    public CampaignLoadStatus(float content, int images, int imageTotal, int buildings, int buildingTotal,
        int heroes, int heroTotal, string stage, string error = null)
    {
        Error = error;
        Ready = error == null && content >= 1 && imageTotal > 0 && images >= imageTotal
            && buildingTotal > 0 && buildings >= buildingTotal && heroTotal > 0 && heroes >= heroTotal;
        Progress = Ready ? 1f : Clamp(.25f * Clamp(content) + .2f * Ratio(images, imageTotal)
            + .35f * Ratio(buildings, buildingTotal) + .2f * Ratio(heroes, heroTotal));
        Label = error != null ? "The campaign could not open" : Ready ? "The Hamlet is ready"
            : content < 1 ? stage : images < imageTotal || imageTotal == 0 ? $"Preparing town textures   {images} / {imageTotal}"
            : buildings < buildingTotal || buildingTotal == 0 ? $"Preparing the Hamlet   {buildings} / {buildingTotal}"
            : $"Preparing hero portraits   {heroes} / {heroTotal}";
    }

    private static float Ratio(int completed, int total) => total > 0 ? Clamp(completed / (float)total) : 0;
    private static float Clamp(float value) => Math.Max(0, Math.Min(1, value));
}
