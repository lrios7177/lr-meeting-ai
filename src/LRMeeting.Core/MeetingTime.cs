namespace LRMeeting.Core;
public static class MeetingTime
{
    public static string Format(double milliseconds)
    {
        var time=TimeSpan.FromMilliseconds(Math.Max(0,milliseconds));
        return $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }
}
