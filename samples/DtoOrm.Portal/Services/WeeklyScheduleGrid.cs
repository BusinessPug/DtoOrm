using System.Globalization;

namespace DtoOrm.Portal.Services;

public sealed record WeeklyScheduleGrid(
    IReadOnlyList<ScheduleTimeSlot> TimeSlots,
    IReadOnlyList<ScheduleGridBlock> Blocks,
    int QuarterHourRows);

public sealed record ScheduleTimeSlot(string Label, int RowStart);

public sealed record ScheduleGridBlock(
    ScheduleItem Item,
    int ColumnStart,
    int RowStart,
    int RowSpan,
    int Lane = 0,
    int Lanes = 1,
    bool HasConflict = false)
{
    public string Style
    {
        get
        {
            var width = (100.0 / Math.Max(1, Lanes)).ToString("0.###", CultureInfo.InvariantCulture);
            var left = (Lane * 100.0 / Math.Max(1, Lanes)).ToString("0.###", CultureInfo.InvariantCulture);
            return $"grid-column:{ColumnStart};grid-row:{RowStart} / span {RowSpan};--lane-width:{width}%;--lane-left:{left}%;";
        }
    }
}

public static class WeeklyScheduleGridBuilder
{
    private static readonly TimeSpan DefaultStart = TimeSpan.FromHours(8);
    private static readonly TimeSpan DefaultEnd = TimeSpan.FromHours(17);
    private static readonly TimeSpan QuarterHour = TimeSpan.FromMinutes(15);

    public static WeeklyScheduleGrid Build(IEnumerable<ScheduleItem> schedule)
    {
        var items = schedule
            .Where(item => item.DayOfWeek is >= 1 and <= 5)
            .OrderBy(item => item.DayOfWeek)
            .ThenBy(item => item.StartsAt)
            .ToList();

        var start = DefaultStart;
        var end = DefaultEnd;

        if (items.Count > 0)
        {
            var earliest = items.Min(item => item.StartsAt);
            var latest = items.Max(item => item.EndsAt);
            if (earliest < start)
            {
                start = TimeSpan.FromHours(Math.Floor(earliest.TotalHours));
            }
            if (latest > end)
            {
                end = TimeSpan.FromHours(Math.Ceiling(latest.TotalHours));
            }
        }

        var rowCount = Math.Max(1, (int)Math.Ceiling((end - start).TotalMinutes / QuarterHour.TotalMinutes));
        var slots = new List<ScheduleTimeSlot>();
        for (var slot = start; slot < end; slot = slot.Add(TimeSpan.FromHours(1)))
        {
            var rowStart = 2 + (int)Math.Round((slot - start).TotalMinutes / QuarterHour.TotalMinutes);
            slots.Add(new ScheduleTimeSlot(slot.ToString(@"hh\:mm"), rowStart));
        }

        var blocks = items.Select(item =>
        {
            var rowStart = 2 + (int)Math.Round((item.StartsAt - start).TotalMinutes / QuarterHour.TotalMinutes);
            var rowSpan = Math.Max(2, (int)Math.Ceiling((item.EndsAt - item.StartsAt).TotalMinutes / QuarterHour.TotalMinutes));
            return new ScheduleGridBlock(item, item.DayOfWeek + 1, rowStart, rowSpan);
        }).ToList();

        return new WeeklyScheduleGrid(slots, AssignOverlapLanes(blocks), rowCount);
    }

    private static IReadOnlyList<ScheduleGridBlock> AssignOverlapLanes(IReadOnlyList<ScheduleGridBlock> blocks)
    {
        var result = new List<ScheduleGridBlock>();

        foreach (var day in blocks.GroupBy(block => block.ColumnStart))
        {
            var working = day
                .OrderBy(block => block.RowStart)
                .ThenBy(block => block.RowStart + block.RowSpan)
                .Select(block => new LaneBlock(block))
                .ToList();

            var active = new List<LaneBlock>();
            foreach (var block in working)
            {
                active.RemoveAll(item => item.RowEnd <= block.RowStart);
                var lane = 0;
                while (active.Any(item => item.Lane == lane))
                {
                    lane++;
                }

                block.Lane = lane;
                active.Add(block);
            }

            foreach (var block in working)
            {
                var overlaps = working
                    .Where(other => !ReferenceEquals(other, block) && other.RowStart < block.RowEnd && block.RowStart < other.RowEnd)
                    .ToList();

                var lanes = overlaps.Count == 0
                    ? 1
                    : Math.Max(block.Lane, overlaps.Max(other => other.Lane)) + 1;

                result.Add(block.Source with
                {
                    Lane = block.Lane,
                    Lanes = lanes,
                    HasConflict = overlaps.Count > 0
                });
            }
        }

        return result
            .OrderBy(block => block.ColumnStart)
            .ThenBy(block => block.RowStart)
            .ThenBy(block => block.Lane)
            .ToList();
    }

    private sealed class LaneBlock
    {
        public LaneBlock(ScheduleGridBlock source) => Source = source;

        public ScheduleGridBlock Source { get; }
        public int RowStart => Source.RowStart;
        public int RowEnd => Source.RowStart + Source.RowSpan;
        public int Lane { get; set; }
    }
}
