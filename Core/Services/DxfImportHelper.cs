using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Core.Models;

namespace Core.Services
{
    /// <summary>
    /// DXF 统一导入服务实现类
    /// 提供 DXF 文件导入、图元过滤、离散化和点位提取的一站式功能
    /// 保证 CadPointEditorViewModel 和 CadAlignmentViewModel 使用完全相同的导入逻辑
    /// </summary>
    public class DxfImportHelper : IDxfImportHelper
    {
        private readonly IDxfParserService _dxfParser;

        public DxfImportHelper(IDxfParserService dxfParser)
        {
            _dxfParser = dxfParser ?? throw new ArgumentNullException(nameof(dxfParser));
        }

        /// <summary>
        /// 统一导入 DXF 文件并返回标准化结果
        /// 处理流程：
        ///   1. 调用 IDxfParserService.Parse() 解析文件
        ///   2. 根据 DxfImportOptions 过滤实体类型
        ///   3. 可选：对每个实体进行离散化（设置到 Tag 属性）
        ///   4. 可选：提取原始点位数据
        /// </summary>
        public DxfImportResult Import(string filePath, DxfImportOptions options)
        {
            var result = new DxfImportResult();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return result;

            try
            {
                // 步骤1: 解析 DXF 文件
                result.ParseResult = _dxfParser.Parse(filePath);

                if (!result.ParseResult.IsSuccess)
                    return result;

                // 步骤2: 根据选项过滤并收集实体
                var displayEntities = new ObservableCollection<CadEntity>();
                foreach (var layerPair in result.ParseResult.Layers)
                {
                    string layerName = layerPair.Key;
                    var entities = layerPair.Value;

                    foreach (var entity in entities)
                    {
                        entity.LayerName = layerName;

                        if (!ShouldIncludeEntity(entity, options))
                            continue;

                        // 可选：进行预离散化（结果存入 Tag 属性供 ToHObject 使用）
                        if (options.DiscretizePitchMM > 0)
                        {
                            try
                            {
                                var points = _dxfParser.Discretize(entity, options.DiscretizePitchMM);
                                if (points != null && points.Count > 0)
                                    entity.Tag = points;
                            }
                            catch { }
                        }

                        displayEntities.Add(entity);
                    }
                }

                result.DisplayEntities = displayEntities;
                result.LayerNames = new List<string>(result.ParseResult.LayerNames);

                // 步骤4: 可选提取点位数据
                if (options.ExtractPoints)
                {
                    // 首先尝试从 VERTEX 实体提取（传统POLYLINE方式）
                    var vertexPoints = ExtractPointsFromFile(filePath, options.PointLayerFilter);

                    // ✅ 如果没有 VERTEX，从所有离散化实体生成点位
                    // 适用于 AutoCAD 2018 DXF 格式（只有 LINE/ARC/SPLINE/CIRCLE，无 POLYLINE）
                    if (vertexPoints.Count == 0 && displayEntities.Count > 0)
                    {
                        vertexPoints = GeneratePointsFromEntities(displayEntities);
                    }

                    result.ExtractedPoints = vertexPoints;
                }

                return result;
            }
            catch
            {
                return result;
            }
        }

        private bool ShouldIncludeEntity(CadEntity entity, DxfImportOptions options)
        {
            return entity switch
            {
                CadArc arc => options.IncludeArcs,
                CadCircle circle => options.IncludeCircles,
                CadSpline spline => options.IncludeSplines,
                _ => true
            };
        }

        private List<CadPoint> ExtractPointsFromFile(string filePath, string layerFilter)
        {
            var points = new List<CadPoint>();
            bool inEntities = false;
            bool isVertex = false;
            string currentLayer = "";
            double currentX = 0, currentY = 0, currentZ = 0;
            string groupCode = "";
            bool extractAllLayers = string.IsNullOrEmpty(layerFilter);

            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string trimmed = line.Trim();
                        if (trimmed == "ENTITIES") { inEntities = true; continue; }
                        if (trimmed == "ENDSEC") { inEntities = false; isVertex = false; continue; }
                        if (!inEntities) continue;

                        // ✅ 修正：与 DxfParser.ExtractPoints() 保持完全一致的解析逻辑
                        if (groupCode == "")
                        {
                            groupCode = trimmed;
                            if (groupCode == "0")
                            {
                                // 读取实体类型（下一行）
                                string entityType = reader.ReadLine()?.Trim() ?? "";

                                if (entityType == "VERTEX")
                                {
                                    // 开始新的 VERTEX 块
                                    isVertex = true;
                                    currentLayer = "";
                                    currentX = currentY = currentZ = 0;
                                }
                                else if (entityType == "SEQEND")
                                {
                                    // ✅ VERTEX 序列结束，保存最后一个点位
                                    if (isVertex && (extractAllLayers || currentLayer == layerFilter))
                                    {
                                        int id = points.Count + 1;
                                        points.Add(new CadPoint
                                        {
                                            Id = id.ToString(),
                                            X = Math.Round(currentX, 3),
                                            Y = Math.Round(currentY, 3),
                                            Z = Math.Round(currentZ, 3),
                                            AssySite = ""
                                        });
                                    }
                                    isVertex = false;
                                }
                                else if (isVertex)
                                {
                                    // 遇到其他实体，VERTEX 序列被打断，保存当前点位
                                    if (extractAllLayers || currentLayer == layerFilter)
                                    {
                                        int id = points.Count + 1;
                                        points.Add(new CadPoint
                                        {
                                            Id = id.ToString(),
                                            X = Math.Round(currentX, 3),
                                            Y = Math.Round(currentY, 3),
                                            Z = Math.Round(currentZ, 3),
                                            AssySite = ""
                                        });
                                    }
                                    isVertex = false;
                                }

                                groupCode = "";
                                continue;
                            }
                        }

                        // 读取组码值
                        string value = trimmed;
                        switch (groupCode)
                        {
                            case "8": currentLayer = value; break;
                            case "10": double.TryParse(value, out currentX); break;
                            case "20": double.TryParse(value, out currentY); break;
                            case "30": double.TryParse(value, out currentZ); break;
                            default: break;
                        }
                        groupCode = "";
                    }
                }
            }
            catch { }

            return points;
        }

        /// <summary>
        /// 从所有已离散化的实体生成点位列表
        /// 当 DXF 文件中没有 VERTEX/POLYLINE 实体时使用此方法
        /// 适用于 AutoCAD 2018 DXF 格式（只有 LINE/ARC/SPLINE/CIRCLE 等独立实体）
        /// </summary>
        private List<CadPoint> GeneratePointsFromEntities(ObservableCollection<CadEntity> entities)
        {
            var points = new List<CadPoint>();
            int id = 1;

            foreach (var entity in entities)
            {
                // 检查实体是否有预离散化的点集（存储在 Tag 属性中）
                if (entity.Tag is System.Collections.Generic.List<CadPoint> discretizedPoints)
                {
                    foreach (var pt in discretizedPoints)
                    {
                        points.Add(new CadPoint
                        {
                            Id = id.ToString(),
                            X = Math.Round(pt.X, 3),
                            Y = Math.Round(pt.Y, 3),
                            Z = Math.Round(pt.Z, 3),
                            AssySite = entity.LayerName ?? ""
                        });
                        id++;
                    }
                }
                else
                {
                    // 如果没有预离散化，根据实体类型直接提取关键点
                    switch (entity)
                    {
                        case CadLine line:
                            points.Add(new CadPoint { Id = id.ToString(), X = Math.Round(line.StartX, 3), Y = Math.Round(line.StartY, 3), AssySite = entity.LayerName });
                            id++;
                            points.Add(new CadPoint { Id = id.ToString(), X = Math.Round(line.EndX, 3), Y = Math.Round(line.EndY, 3), AssySite = entity.LayerName });
                            id++;
                            break;

                        case CadArc arc:
                            // 添加圆弧的起点和终点
                            double startRad = arc.StartAngle * Math.PI / 180;
                            double endRad = arc.EndAngle * Math.PI / 180;
                            points.Add(new CadPoint
                            {
                                Id = id.ToString(),
                                X = Math.Round(arc.CenterX + arc.Radius * Math.Cos(startRad), 3),
                                Y = Math.Round(arc.CenterY + arc.Radius * Math.Sin(startRad), 3),
                                AssySite = entity.LayerName
                            });
                            id++;
                            points.Add(new CadPoint
                            {
                                Id = id.ToString(),
                                X = Math.Round(arc.CenterX + arc.Radius * Math.Cos(endRad), 3),
                                Y = Math.Round(arc.CenterY + arc.Radius * Math.Sin(endRad), 3),
                                AssySite = entity.LayerName
                            });
                            id++;
                            break;

                        case CadCircle circle:
                            // 添加圆心点
                            points.Add(new CadPoint { Id = id.ToString(), X = Math.Round(circle.CenterX, 3), Y = Math.Round(circle.CenterY, 3), AssySite = entity.LayerName });
                            id++;
                            break;

                        default:
                            // 其他实体类型：跳过或添加包围盒中心
                            break;
                    }
                }
            }

            return points;
        }
    }
}
