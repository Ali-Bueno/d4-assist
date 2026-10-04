namespace D4Assist.Update;

/// <summary>Turns a stream of percentages into a call at each milestone passed (25, 50, 75), never at 100 or twice.</summary>
public static class ProgressMilestones
{
    private const int FullPercent = 100;

    /// <summary>A percent sink that calls <paramref name="onMilestone"/> with each multiple of <paramref name="step"/> below 100 once.</summary>
    public static Action<int> Every(int step, Action<int> onMilestone)
    {
        int next = step;
        return percent =>
        {
            if (percent < next || next >= FullPercent) return;
            onMilestone(percent / step * step);
            next = (percent / step + 1) * step;
        };
    }
}
