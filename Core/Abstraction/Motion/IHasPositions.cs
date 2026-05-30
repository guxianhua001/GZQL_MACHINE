using Core.Models;
using System.Collections.Generic;

namespace Core.Abstraction
{
    /// <summary>
    /// 标记接口：表示该工站参数对象包含位置字典
    /// </summary>
    public interface IHasPositions
    {
        Dictionary<string, FlexiblePosition> Positions { get; set; }
    }
}
