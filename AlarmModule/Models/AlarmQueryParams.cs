using System;
using System.Collections.Generic;

namespace AlarmModule.Models
{
    /// <summary>
    /// 报警查询参数：支持多条件过滤
    /// </summary>
    public class AlarmQueryParams
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public AlarmLevel? Level { get; set; }
        public string? Source { get; set; }
        public AlarmStatus? Status { get; set; }
        public AlarmType? Type { get; set; }
        public string? Keyword { get; set; }
    }

    /// <summary>
    /// 分页查询结果
    /// </summary>
    public class PagedResult<T>
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public List<T> Items { get; set; } = new List<T>();
    }
}
