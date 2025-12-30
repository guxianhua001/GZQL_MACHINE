using Interfaces;
using System.Collections.Generic;
using System;

public class PagedResult<T>
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public List<T> Items { get; set; }
}

public class AlarmQueryParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public AlarmLevel? Level { get; set; }
    public int? StationId { get; set; }
}
