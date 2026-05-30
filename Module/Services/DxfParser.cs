// Services/DxfParser.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Module.Services
{
    public static class DxfParser
    {
        /// <summary>
        /// 从 DXF 文件提取指定层的 VERTEX 点坐标
        /// </summary>
        /// <param name="dxfPath">DXF 文件路径</param>
        /// <param name="layerName">图层名称（如 "T001L001"）</param>
        /// <returns>点列表 (X, Y, Z)</returns>
        public static List<(double X, double Y, double Z)> ExtractPoints(string dxfPath, string layerName)
        {
            var points = new List<(double X, double Y, double Z)>();
            bool inEntities = false;
            bool isVertex = false;
            string currentLayer = "";
            double currentX = 0, currentY = 0, currentZ = 0;
            string groupCode = "";
            bool extractAllLayers = string.IsNullOrEmpty(layerName);

            using (var reader = new StreamReader(dxfPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed == "ENTITIES")
                    {
                        inEntities = true;
                        continue;
                    }
                    if (trimmed == "ENDSEC")
                    {
                        inEntities = false;
                        isVertex = false;
                        continue;
                    }
                    if (!inEntities) continue;

                    // 组码解析逻辑
                    if (groupCode == "")
                    {
                        groupCode = trimmed;
                        if (groupCode == "0")
                        {
                            string entityType = reader.ReadLine()?.Trim();
                            if (entityType == "VERTEX")
                            {
                                isVertex = true;
                                currentLayer = "";
                                currentX = currentY = currentZ = 0;
                            }
                            else if (entityType == "SEQEND")
                            {
                                if (isVertex && (extractAllLayers || currentLayer == layerName))
                                    points.Add((currentX, currentY, currentZ));
                                isVertex = false;
                            }
                            else if (isVertex)
                            {
                                // 其他实体打断 VERTEX
                                if (extractAllLayers || currentLayer == layerName)
                                    points.Add((currentX, currentY, currentZ));
                                isVertex = false;
                            }
                            groupCode = "";
                        }
                    }
                    else
                    {
                        string value = trimmed;
                        if (isVertex)
                        {
                            switch (groupCode)
                            {
                                case "8":
                                    currentLayer = value;
                                    break;
                                case "10":
                                    currentX = double.Parse(value, CultureInfo.InvariantCulture);
                                    break;
                                case "20":
                                    currentY = double.Parse(value, CultureInfo.InvariantCulture);
                                    break;
                                case "30":
                                    currentZ = double.Parse(value, CultureInfo.InvariantCulture);
                                    if (extractAllLayers || currentLayer == layerName)
                                        points.Add((currentX, currentY, currentZ));
                                    break;
                            }
                        }
                        groupCode = "";
                    }
                }
            }
            return points;
        }
    }
}