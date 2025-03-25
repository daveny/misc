using System;
using System.Collections.Generic;

namespace WebApplication6.Helpers
{
    public static class FormatParser
    {
        public static FormatOptions ParseFormattingOptions(string formattingString)
        {
            FormatOptions options = new FormatOptions();

            if (string.IsNullOrWhiteSpace(formattingString))
                return options;

            try
            {
                // Remove any curly braces and parse as JSON if possible
                formattingString = formattingString.Trim();
                if (formattingString.StartsWith("{") && formattingString.EndsWith("}"))
                {
                    formattingString = formattingString.Substring(1, formattingString.Length - 2);
                }

                // Parse row pattern settings
                if (formattingString.Contains("row:"))
                {
                    int rowStart = formattingString.IndexOf("row:") + 4;
                    int rowEnd = FindMatchingClosingBrace(formattingString, rowStart);
                    if (rowEnd > rowStart)
                    {
                        string rowOptions = formattingString.Substring(rowStart, rowEnd - rowStart).Trim();

                        // Extract index
                        int indexStart = rowOptions.IndexOf("index:") + 6;
                        int indexEnd = rowOptions.IndexOf(",", indexStart);
                        if (indexEnd == -1) indexEnd = rowOptions.Length;

                        // Extract style
                        int styleStart = rowOptions.IndexOf("style:") + 6;
                        int styleEnd = rowOptions.IndexOf(",", styleStart);
                        if (styleEnd == -1) styleEnd = rowOptions.Length;

                        if (indexStart > 6 && indexEnd > indexStart &&
                            styleStart > 6 && styleEnd > styleStart)
                        {
                            string indexStr = rowOptions.Substring(indexStart, indexEnd - indexStart).Trim();
                            string styleStr = rowOptions.Substring(styleStart, styleEnd - styleStart).Trim();

                            // Clean up strings (remove quotes)
                            if (styleStr.StartsWith("\"") && styleStr.EndsWith("\""))
                                styleStr = styleStr.Substring(1, styleStr.Length - 2);

                            options.RowPattern = new RowPatternOptions
                            {
                                Index = int.Parse(indexStr),
                                Style = styleStr
                            };
                        }
                    }
                }

                // Parse column pattern settings
                if (formattingString.Contains("column:"))
                {
                    int colStart = formattingString.IndexOf("column:") + 7;
                    int colEnd = FindMatchingClosingBrace(formattingString, colStart);
                    if (colEnd > colStart)
                    {
                        string colOptions = formattingString.Substring(colStart, colEnd - colStart).Trim();

                        // Extract nameContains
                        int nameStart = colOptions.IndexOf("nameContains:") + 13;
                        int nameEnd = colOptions.IndexOf(",", nameStart);
                        if (nameEnd == -1) nameEnd = colOptions.Length;

                        // Extract style
                        int styleStart = colOptions.IndexOf("style:") + 6;
                        int styleEnd = colOptions.IndexOf(",", styleStart);
                        if (styleEnd == -1) styleEnd = colOptions.Length;

                        if (nameStart > 13 && nameEnd > nameStart &&
                            styleStart > 6 && styleEnd > styleStart)
                        {
                            string nameStr = colOptions.Substring(nameStart, nameEnd - nameStart).Trim();
                            string styleStr = colOptions.Substring(styleStart, styleEnd - styleStart).Trim();

                            // Clean up strings (remove quotes)
                            if (nameStr.StartsWith("\"") && nameStr.EndsWith("\""))
                                nameStr = nameStr.Substring(1, nameStr.Length - 2);
                            if (styleStr.StartsWith("\"") && styleStr.EndsWith("\""))
                                styleStr = styleStr.Substring(1, styleStr.Length - 2);

                            options.ColumnPattern = new ColumnPatternOptions
                            {
                                NameContains = nameStr,
                                Style = styleStr
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't throw it - we'll return default options
                System.Diagnostics.Debug.WriteLine($"Error parsing formatting options: {ex.Message}");
            }

            return options;
        }

        public static BarChartOptions ParseBarChartOptions(Dictionary<string, string> instructions)
        {
            var options = new BarChartOptions
            {
                ChartTitle = "Bar Chart",
                BorderWidth = 1,
                Horizontal = false,
                Stacked = false,
                BackgroundColors = GetDefaultBackgroundColors(),
                BorderColors = GetDefaultBorderColors()
            };

            // Set sort options
            if (instructions.ContainsKey("sortBy"))
            {
                options.SortBy = instructions["sortBy"];
            }

            if (instructions.ContainsKey("sortDirection"))
            {
                options.SortDirection = instructions["sortDirection"].ToLower();
            }

            // Extract formatting options if available
            if (instructions.ContainsKey("formatting"))
            {
                string formattingStr = instructions["formatting"];

                if (formattingStr.Contains("title:"))
                {
                    int start = formattingStr.IndexOf("title:") + 6;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        options.ChartTitle = formattingStr.Substring(start, end - start).Trim();
                        if (options.ChartTitle.StartsWith("\"") && options.ChartTitle.EndsWith("\""))
                            options.ChartTitle = options.ChartTitle.Substring(1, options.ChartTitle.Length - 2);
                    }
                }

                if (formattingStr.Contains("borderWidth:"))
                {
                    int start = formattingStr.IndexOf("borderWidth:") + 12;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string borderWidthStr = formattingStr.Substring(start, end - start).Trim();
                        int.TryParse(borderWidthStr, out int borderWidth);
                        options.BorderWidth = borderWidth;
                    }
                }

                if (formattingStr.Contains("horizontal:"))
                {
                    int start = formattingStr.IndexOf("horizontal:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string horizontalStr = formattingStr.Substring(start, end - start).Trim();
                        bool.TryParse(horizontalStr, out bool horizontal);
                        options.Horizontal = horizontal;
                    }
                }

                if (formattingStr.Contains("stacked:"))
                {
                    int start = formattingStr.IndexOf("stacked:") + 8;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string stackedStr = formattingStr.Substring(start, end - start).Trim();
                        bool.TryParse(stackedStr, out bool stacked);
                        options.Stacked = stacked;
                    }
                }

                // Parse format options for rows and columns
                options.FormatOptions = ParseFormattingOptions(formattingStr);
            }

            return options;
        }

        public static LineChartOptions ParseLineChartOptions(Dictionary<string, string> instructions)
        {
            var options = new LineChartOptions
            {
                ChartTitle = "Line Chart",
                ShowPoints = true,
                Tension = 0,
                Colors = GetDefaultColors()
            };

            // Set sort options
            if (instructions.ContainsKey("sortBy"))
            {
                options.SortBy = instructions["sortBy"];
            }

            if (instructions.ContainsKey("sortDirection"))
            {
                options.SortDirection = instructions["sortDirection"].ToLower();
            }

            // Extract formatting options if available
            if (instructions.ContainsKey("formatting"))
            {
                string formattingStr = instructions["formatting"];

                if (formattingStr.Contains("title:"))
                {
                    int start = formattingStr.IndexOf("title:") + 6;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        options.ChartTitle = formattingStr.Substring(start, end - start).Trim();
                        if (options.ChartTitle.StartsWith("\"") && options.ChartTitle.EndsWith("\""))
                            options.ChartTitle = options.ChartTitle.Substring(1, options.ChartTitle.Length - 2);
                    }
                }

                if (formattingStr.Contains("showPoints:"))
                {
                    int start = formattingStr.IndexOf("showPoints:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showPointsStr = formattingStr.Substring(start, end - start).Trim();
                        bool.TryParse(showPointsStr, out bool showPoints);
                        options.ShowPoints = showPoints;
                    }
                }

                if (formattingStr.Contains("tension:"))
                {
                    int start = formattingStr.IndexOf("tension:") + 8;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string tensionStr = formattingStr.Substring(start, end - start).Trim();
                        int.TryParse(tensionStr, out int tension);
                        options.Tension = tension;
                    }
                }

                // Parse format options for rows and columns
                options.FormatOptions = ParseFormattingOptions(formattingStr);
            }

            return options;
        }

        public static PieChartOptions ParsePieChartOptions(Dictionary<string, string> instructions)
        {
            var options = new PieChartOptions
            {
                ChartTitle = "Pie Chart",
                ShowLegend = true,
                IsDoughnut = false,
                ShowValues = true,
                ShowPercentages = true,
                ValuePosition = "legend",
                BackgroundColors = GetDefaultBackgroundColors(),
                BorderColors = GetDefaultBorderColors(),
                SortDirection = "desc" // Default to descending for pie charts (largest first)
            };

            // Set sort options
            if (instructions.ContainsKey("sortBy"))
            {
                options.SortBy = instructions["sortBy"];
            }

            if (instructions.ContainsKey("sortDirection"))
            {
                options.SortDirection = instructions["sortDirection"].ToLower();
            }

            // Extract formatting options if available
            if (instructions.ContainsKey("formatting"))
            {
                string formattingStr = instructions["formatting"];

                if (formattingStr.Contains("title:"))
                {
                    int start = formattingStr.IndexOf("title:") + 6;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        options.ChartTitle = formattingStr.Substring(start, end - start).Trim();
                        if (options.ChartTitle.StartsWith("\"") && options.ChartTitle.EndsWith("\""))
                            options.ChartTitle = options.ChartTitle.Substring(1, options.ChartTitle.Length - 2);
                    }
                }

                if (formattingStr.Contains("showLegend:"))
                {
                    int start = formattingStr.IndexOf("showLegend:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showLegendStr = formattingStr.Substring(start, end - start).Trim();
                        bool.TryParse(showLegendStr, out bool showLegend);
                        options.ShowLegend = showLegend;
                    }
                }

                if (formattingStr.Contains("doughnut:"))
                {
                    int start = formattingStr.IndexOf("doughnut:") + 9;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string doughnutStr = formattingStr.Substring(start, end - start).Trim();
                        bool.TryParse(doughnutStr, out bool isDoughnut);
                        options.IsDoughnut = isDoughnut;
                    }
                }

                // Parse value display options
                if (formattingStr.Contains("showValues:"))
                {
                    int start = formattingStr.IndexOf("showValues:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showValuesStr = formattingStr.Substring(start, end - start).Trim();
                        bool.TryParse(showValuesStr, out bool showValues);
                        options.ShowValues = showValues;
                    }
                }

                if (formattingStr.Contains("showPercentages:"))
                {
                    int start = formattingStr.IndexOf("showPercentages:") + 16;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showPercentagesStr = formattingStr.Substring(start, end - start).Trim();
                        bool.TryParse(showPercentagesStr, out bool showPercentages);
                        options.ShowPercentages = showPercentages;
                    }
                }

                if (formattingStr.Contains("valuePosition:"))
                {
                    int start = formattingStr.IndexOf("valuePosition:") + 14;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string valuePositionStr = formattingStr.Substring(start, end - start).Trim();
                        if (valuePositionStr.StartsWith("\"") && valuePositionStr.EndsWith("\""))
                            valuePositionStr = valuePositionStr.Substring(1, valuePositionStr.Length - 2);

                        if (valuePositionStr == "inside" || valuePositionStr == "outside" || valuePositionStr == "legend")
                        {
                            options.ValuePosition = valuePositionStr;
                        }
                    }
                }

                // Parse format options for rows and columns
                options.FormatOptions = ParseFormattingOptions(formattingStr);
            }

            return options;
        }

        // Helper method to find matching closing brace
        private static int FindMatchingClosingBrace(string text, int openBraceIndex)
        {
            int braceCount = 1;
            for (int i = openBraceIndex + 1; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    braceCount++;
                }
                else if (text[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        // Default colors for charts
        private static string[] GetDefaultBackgroundColors()
        {
            return new string[]
            {
                "rgba(75, 192, 192, 0.2)",
                "rgba(255, 99, 132, 0.2)",
                "rgba(54, 162, 235, 0.2)",
                "rgba(255, 206, 86, 0.2)",
                "rgba(153, 102, 255, 0.2)",
                "rgba(255, 159, 64, 0.2)",
                "rgba(201, 203, 207, 0.2)",
                "rgba(100, 149, 237, 0.2)"
            };
        }

        private static string[] GetDefaultBorderColors()
        {
            return new string[]
            {
                "rgba(75, 192, 192, 1)",
                "rgba(255, 99, 132, 1)",
                "rgba(54, 162, 235, 1)",
                "rgba(255, 206, 86, 1)",
                "rgba(153, 102, 255, 1)",
                "rgba(255, 159, 64, 1)",
                "rgba(201, 203, 207, 1)",
                "rgba(100, 149, 237, 1)"
            };
        }

        private static string[] GetDefaultColors()
        {
            return new string[]
            {
                "rgba(75, 192, 192, 1)",
                "rgba(255, 99, 132, 1)",
                "rgba(54, 162, 235, 1)",
                "rgba(255, 206, 86, 1)",
                "rgba(153, 102, 255, 1)",
                "rgba(255, 159, 64, 1)",
                "rgba(201, 203, 207, 1)",
                "rgba(100, 149, 237, 1)"
            };
        }
    }
}