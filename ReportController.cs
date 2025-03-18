using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace WebApplication6
{
    public class ReportController : Controller
    {
        private readonly string _connectionString;

        public ReportController()
        {
            _connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
        }

        public ActionResult RenderReport(string templateName)
        {
            try
            {
                // 1. Load the THTML template
                string templatePath = Server.MapPath($"~/Views/Reports/Templates/{templateName}.thtml");
                string templateContent = System.IO.File.ReadAllText(templatePath);

                // 2. Process the template and render the final result
                string renderedContent = ProcessTemplate(templateContent);

                // 3. Pass the content to the view
                ViewBag.ReportContent = renderedContent;
                ViewBag.ReportName = templateName;

                // Return the view (should be in Views/Report/RenderReport.cshtml)
                return View();
            }
            catch (Exception ex)
            {
                // Add logging here
                return Content($"Error: {ex.Message}");
            }
        }

        private string ProcessTemplate(string templateContent)
        {
            // Find all tokens in the template using regex
            string pattern = @"\{\{((?:[^{}]|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!)))\}\}";
            MatchCollection matches = Regex.Matches(templateContent, pattern);

            // Process each token
            foreach (Match match in matches)
            {
                string token = match.Value;
                string instructionContent = match.Groups[1].Value.Trim();

                // Parse instructions from the token
                var instructions = ParseInstructions(instructionContent);

                // Execute the query and get the data
                DataTable data = ExecuteQuery(instructions["query"]);

                // Render the data according to the specified representation and formatting
                string renderedData = RenderData(data, instructions);

                // Replace the token with the rendered content
                templateContent = templateContent.Replace(token, renderedData);
            }

            return templateContent;
        }

        private Dictionary<string, string> ParseInstructions(string instructionContent)
        {
            var instructions = new Dictionary<string, string>();

            // Split by semicolons that are not inside quotes or curly braces
            List<string> parts = new List<string>();
            bool inQuotes = false;
            bool inCurlyBraces = false;
            int startIndex = 0;

            for (int i = 0; i < instructionContent.Length; i++)
            {
                char c = instructionContent[i];

                if (c == '"')
                {
                    // Toggle quote state, but only if not escaped
                    if (i == 0 || instructionContent[i - 1] != '\\')
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == '{')
                {
                    inCurlyBraces = true;
                }
                else if (c == '}')
                {
                    inCurlyBraces = false;
                }
                else if (c == ';' && !inQuotes && !inCurlyBraces)
                {
                    // Found a semicolon outside quotes and curly braces - split here
                    parts.Add(instructionContent.Substring(startIndex, i - startIndex).Trim());
                    startIndex = i + 1;
                }
            }

            // Add the last part
            if (startIndex < instructionContent.Length)
            {
                parts.Add(instructionContent.Substring(startIndex).Trim());
            }

            // Process each part as a key-value pair
            foreach (string part in parts)
            {
                int equalsPos = part.IndexOf('=');
                if (equalsPos > 0)
                {
                    string key = part.Substring(0, equalsPos).Trim();
                    string value = part.Substring(equalsPos + 1).Trim();

                    // Remove surrounding quotes if present
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    // Handle array format for series and legends
                    if ((key == "series" || key == "legends") && value.StartsWith("[") && value.EndsWith("]"))
                    {
                        // Keep the array format as is
                        instructions[key] = value;
                    }
                    else
                    {
                        instructions[key] = value;
                    }
                }
            }

            return instructions;
        }

        private DataTable ExecuteQuery(string query)
        {
            DataTable result = new DataTable();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(result);
                    }
                }
            }

            return result;
        }

        private string RenderData(DataTable data, Dictionary<string, string> instructions)
        {
            string representation = instructions["representation"].ToLower();

            switch (representation)
            {
                case "table":
                    return RenderDataTable(data, instructions);
                case "barchart":
                    return RenderBarChart(data, instructions);
                case "linechart":
                    return RenderLineChart(data, instructions);
                default:
                    return RenderDataTable(data, instructions);
            }
        }

        private string RenderDataTable(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the table
            string tableId = "datatable_" + Guid.NewGuid().ToString("N");

            // Parse formatting instructions if available
            FormatOptions formatOptions = ParseFormattingOptions(instructions.ContainsKey("formatting") ? instructions["formatting"] : "");

            // Build the HTML for the table
            string html = $"<table id='{tableId}' class='display' style='width:100%'><thead><tr>";

            // Add table headers
            foreach (DataColumn column in data.Columns)
            {
                string headerStyle = "";
                if (formatOptions.ColumnPattern != null &&
                    column.ColumnName.Contains(formatOptions.ColumnPattern.NameContains))
                {
                    headerStyle = $" style='{formatOptions.ColumnPattern.Style}'";
                }

                html += $"<th{headerStyle}>{column.ColumnName}</th>";
            }
            html += "</tr></thead><tbody>";

            // Add table rows
            int rowIndex = 0;
            foreach (DataRow row in data.Rows)
            {
                rowIndex++;

                string rowStyle = "";
                if (formatOptions.RowPattern != null &&
                    rowIndex % formatOptions.RowPattern.Index == 0)
                {
                    rowStyle = $" style='{formatOptions.RowPattern.Style}'";
                }

                html += $"<tr{rowStyle}>";

                for (int i = 0; i < row.ItemArray.Length; i++)
                {
                    string cellStyle = "";
                    string columnName = data.Columns[i].ColumnName;

                    if (formatOptions.ColumnPattern != null &&
                        columnName.Contains(formatOptions.ColumnPattern.NameContains))
                    {
                        cellStyle = $" style='{formatOptions.ColumnPattern.Style}'";
                    }

                    html += $"<td{cellStyle}>{row.ItemArray[i]}</td>";
                }

                html += "</tr>";
            }
            html += "</tbody></table>";

            // Add DataTables initialization script with styling options
            html += $@"
<script>
    $(document).ready(function() {{
        $('#{tableId}').DataTable({{
            // Preserve any custom styling when DataTables renders
            'drawCallback': function() {{
                // Apply row styling based on pattern
                if ({(formatOptions.RowPattern != null ? "true" : "false")}) {{
                    $('#{tableId} tbody tr').each(function(index) {{
                        if (((index + 1) % {(formatOptions.RowPattern?.Index ?? 0)}) === 0) {{
                            $(this).attr('style', '{(formatOptions.RowPattern?.Style ?? "")}');
                        }}
                    }});
                }}
                
                // Apply column styling based on name pattern
                if ({(formatOptions.ColumnPattern != null ? "true" : "false")}) {{
                    $('#{tableId} thead th').each(function(index) {{
                        var columnText = $(this).text();
                        if (columnText.indexOf('{(formatOptions.ColumnPattern?.NameContains ?? "")}') !== -1) {{
                            $(this).attr('style', '{(formatOptions.ColumnPattern?.Style ?? "")}');
                            $('#{tableId} tbody tr td:nth-child(' + (index + 1) + ')').attr('style', '{(formatOptions.ColumnPattern?.Style ?? "")}');
                        }}
                    }});
                }}
            }}
        }});
    }});
</script>";

            return html;
        }

        // FormatOptions class
        public class FormatOptions
        {
            public RowPatternOptions RowPattern { get; set; }
            public ColumnPatternOptions ColumnPattern { get; set; }
        }

        public class RowPatternOptions
        {
            public int Index { get; set; }  // Apply style to every nth row
            public string Style { get; set; }
        }

        public class ColumnPatternOptions
        {
            public string NameContains { get; set; }  // Apply style to columns with names containing this string
            public string Style { get; set; }
        }

        // ParseFormattingOptions method to add to ReportController
        private FormatOptions ParseFormattingOptions(string formattingString)
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
                    int rowEnd = formattingString.IndexOf("}", rowStart);
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
                    int colEnd = formattingString.IndexOf("}", colStart);
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

        // RenderLineChart method with support for multiple series
        // Updated RenderBarChart method with multiple Y-axes support
        private string RenderBarChart(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the chart
            string chartId = "barchart_" + Guid.NewGuid().ToString("N");

            // Default configuration
            string chartTitle = "Bar Chart";
            int borderWidth = 1;
            bool horizontal = false;
            bool stacked = false;
            bool multiAxis = false;

            // Define default colors to cycle through
            string[] backgroundColors = new string[]
            {
        "rgba(75, 192, 192, 0.2)",
        "rgba(255, 99, 132, 0.2)",
        "rgba(54, 162, 235, 0.2)",
        "rgba(255, 206, 86, 0.2)",
        "rgba(153, 102, 255, 0.2)"
            };

            string[] borderColors = new string[]
            {
        "rgba(75, 192, 192, 1)",
        "rgba(255, 99, 132, 1)",
        "rgba(54, 162, 235, 1)",
        "rgba(255, 206, 86, 1)",
        "rgba(153, 102, 255, 1)"
            };

            // Get series column names
            List<string> seriesColumns = new List<string>();
            if (instructions.ContainsKey("series"))
            {
                string seriesValue = instructions["series"];
                if (seriesValue.StartsWith("[") && seriesValue.EndsWith("]"))
                {
                    // Parse array format
                    seriesValue = seriesValue.Substring(1, seriesValue.Length - 2);
                    seriesColumns = seriesValue.Split(',')
                        .Select(s => s.Trim().Trim('"', '\''))
                        .ToList();
                }
                else
                {
                    seriesColumns.Add(seriesValue);
                }
            }
            else
            {
                // Default to second column
                seriesColumns.Add(data.Columns[1].ColumnName);
            }

            // Check if we need multiple Y-axes
            Dictionary<string, string> seriesYAxisMap = new Dictionary<string, string>();
            if (instructions.ContainsKey("yAxes"))
            {
                multiAxis = true;
                string yAxesValue = instructions["yAxes"];

                if (yAxesValue.StartsWith("{") && yAxesValue.EndsWith("}"))
                {
                    // Remove the outer braces
                    yAxesValue = yAxesValue.Substring(1, yAxesValue.Length - 2);

                    // Split by commas that are not inside quotes
                    List<string> axisMappings = new List<string>();
                    bool inQuotes = false;
                    int startPos = 0;

                    for (int i = 0; i < yAxesValue.Length; i++)
                    {
                        if (yAxesValue[i] == '"' && (i == 0 || yAxesValue[i - 1] != '\\'))
                        {
                            inQuotes = !inQuotes;
                        }
                        else if (yAxesValue[i] == ',' && !inQuotes)
                        {
                            axisMappings.Add(yAxesValue.Substring(startPos, i - startPos).Trim());
                            startPos = i + 1;
                        }
                    }

                    // Add the last part
                    if (startPos < yAxesValue.Length)
                    {
                        axisMappings.Add(yAxesValue.Substring(startPos).Trim());
                    }

                    // Process each mapping
                    foreach (string mapping in axisMappings)
                    {
                        int colonPos = mapping.IndexOf(':');
                        if (colonPos > 0)
                        {
                            string seriesName = mapping.Substring(0, colonPos).Trim().Trim('"');
                            string axisId = mapping.Substring(colonPos + 1).Trim().Trim('"');
                            seriesYAxisMap[seriesName] = axisId;
                        }
                    }
                }
            }

            // Get legends (defaults to first column for labels on x-axis)
            string legendsColumn = data.Columns[0].ColumnName;
            if (instructions.ContainsKey("legends"))
            {
                string legendsValue = instructions["legends"];
                if (legendsValue.StartsWith("[") && legendsValue.EndsWith("]"))
                {
                    // Multiple legends not supported for x-axis, take the first one
                    legendsValue = legendsValue.Substring(1, legendsValue.Length - 2);
                    string firstLegend = legendsValue.Split(',')
                        .Select(s => s.Trim().Trim('"', '\''))
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(firstLegend))
                    {
                        legendsColumn = firstLegend;
                    }
                }
                else
                {
                    legendsColumn = legendsValue;
                }
            }

            // Get legend column index
            int legendsColumnIndex = 0;
            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (data.Columns[i].ColumnName.Equals(legendsColumn, StringComparison.OrdinalIgnoreCase))
                {
                    legendsColumnIndex = i;
                    break;
                }
            }

            // Dictionary to store Y-axis configuration
            Dictionary<string, Dictionary<string, string>> yAxisConfigs = new Dictionary<string, Dictionary<string, string>>();

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
                        chartTitle = formattingStr.Substring(start, end - start).Trim();
                        if (chartTitle.StartsWith("\"") && chartTitle.EndsWith("\""))
                            chartTitle = chartTitle.Substring(1, chartTitle.Length - 2);
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
                        int.TryParse(borderWidthStr, out borderWidth);
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
                        bool.TryParse(horizontalStr, out horizontal);
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
                        bool.TryParse(stackedStr, out stacked);
                    }
                }

                // Parse Y-axis configurations for multiple axes
                if (multiAxis && formattingStr.Contains("yAxis:"))
                {
                    int yAxisStart = formattingStr.IndexOf("yAxis:") + 6;
                    int yAxisEnd = FindMatchingClosingBrace(formattingStr, yAxisStart);

                    if (yAxisEnd > yAxisStart)
                    {
                        string yAxisStr = formattingStr.Substring(yAxisStart, yAxisEnd - yAxisStart).Trim();

                        // Process each Y-axis configuration
                        int pos = 0;
                        while (pos < yAxisStr.Length)
                        {
                            int idStart = yAxisStr.IndexOf("id:", pos) + 3;
                            if (idStart < 3) break; // No more IDs found

                            int idEnd = yAxisStr.IndexOf(",", idStart);
                            if (idEnd == -1) break;

                            string axisId = yAxisStr.Substring(idStart, idEnd - idStart).Trim().Trim('"');

                            int configStart = yAxisStr.IndexOf("{", idEnd) + 1;
                            int configEnd = FindMatchingClosingBrace(yAxisStr, configStart - 1);

                            if (configEnd > configStart)
                            {
                                string configStr = yAxisStr.Substring(configStart, configEnd - configStart).Trim();
                                Dictionary<string, string> axisConfig = new Dictionary<string, string>();

                                // Parse position
                                int positionStart = configStr.IndexOf("position:") + 9;
                                if (positionStart > 9)
                                {
                                    int positionEnd = configStr.IndexOf(",", positionStart);
                                    if (positionEnd == -1) positionEnd = configStr.Length;
                                    string position = configStr.Substring(positionStart, positionEnd - positionStart).Trim().Trim('"');
                                    axisConfig["position"] = position;
                                }

                                // Parse title
                                int titleStart = configStr.IndexOf("title:") + 6;
                                if (titleStart > 6)
                                {
                                    int titleEnd = configStr.IndexOf(",", titleStart);
                                    if (titleEnd == -1) titleEnd = configStr.Length;
                                    string title = configStr.Substring(titleStart, titleEnd - titleStart).Trim().Trim('"');
                                    axisConfig["title"] = title;
                                }

                                // Parse color
                                int colorStart = configStr.IndexOf("color:") + 6;
                                if (colorStart > 6)
                                {
                                    int colorEnd = configStr.IndexOf(",", colorStart);
                                    if (colorEnd == -1) colorEnd = configStr.Length;
                                    string color = configStr.Substring(colorStart, colorEnd - colorStart).Trim().Trim('"');
                                    axisConfig["color"] = color;
                                }

                                // Store the configuration
                                yAxisConfigs[axisId] = axisConfig;

                                // Move to the next axis configuration
                                pos = configEnd + 1;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }

            // Extract labels for x-axis
            List<string> labels = new List<string>();
            foreach (DataRow row in data.Rows)
            {
                labels.Add(row[legendsColumnIndex].ToString());
            }

            // Prepare datasets for each series
            List<string> datasets = new List<string>();
            HashSet<string> usedYAxisIds = new HashSet<string>();

            for (int i = 0; i < seriesColumns.Count; i++)
            {
                string seriesName = seriesColumns[i];
                int seriesColumnIndex = -1;

                // Find column index by name
                for (int j = 0; j < data.Columns.Count; j++)
                {
                    if (data.Columns[j].ColumnName.Equals(seriesName, StringComparison.OrdinalIgnoreCase))
                    {
                        seriesColumnIndex = j;
                        break;
                    }
                }

                if (seriesColumnIndex == -1) continue; // Skip if column not found

                // Extract values for this series
                List<double> values = new List<double>();
                foreach (DataRow row in data.Rows)
                {
                    values.Add(Convert.ToDouble(row[seriesColumnIndex]));
                }

                // Use current series color from the palette
                string backgroundColor = backgroundColors[i % backgroundColors.Length];
                string borderColor = borderColors[i % borderColors.Length];

                // Custom colors can be added from formatting
                if (instructions.ContainsKey("formatting"))
                {
                    string formattingStr = instructions["formatting"];

                    if (formattingStr.Contains($"backgroundColor{i}:"))
                    {
                        int start = formattingStr.IndexOf($"backgroundColor{i}:") + 15 + i.ToString().Length;
                        int end = formattingStr.IndexOf(",", start);
                        if (end == -1) end = formattingStr.IndexOf("}", start);
                        if (end > start)
                        {
                            backgroundColor = formattingStr.Substring(start, end - start).Trim();
                            if (backgroundColor.StartsWith("\"") && backgroundColor.EndsWith("\""))
                                backgroundColor = backgroundColor.Substring(1, backgroundColor.Length - 2);
                        }
                    }

                    if (formattingStr.Contains($"borderColor{i}:"))
                    {
                        int start = formattingStr.IndexOf($"borderColor{i}:") + 12 + i.ToString().Length;
                        int end = formattingStr.IndexOf(",", start);
                        if (end == -1) end = formattingStr.IndexOf("}", start);
                        if (end > start)
                        {
                            borderColor = formattingStr.Substring(start, end - start).Trim();
                            if (borderColor.StartsWith("\"") && borderColor.EndsWith("\""))
                                borderColor = borderColor.Substring(1, borderColor.Length - 2);
                        }
                    }
                }

                // Determine Y-axis for this series
                string yAxisId = "y";
                if (multiAxis && seriesYAxisMap.ContainsKey(seriesName))
                {
                    yAxisId = seriesYAxisMap[seriesName];
                    usedYAxisIds.Add(yAxisId);
                }

                // Create dataset JSON for this series
                string dataset = $@"{{
            label: '{seriesName}',
            data: {JsonConvert.SerializeObject(values)},
            backgroundColor: '{backgroundColor}',
            borderColor: '{borderColor}',
            borderWidth: {borderWidth},
            yAxisID: '{yAxisId}'
        }}";

                datasets.Add(dataset);
            }

            // Create the HTML container for the chart
            string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";

            // Build the scales configuration for Chart.js
            string scalesConfig = "scales: {";

            // Add x-axis configuration
            scalesConfig += $@"
        x: {{
            stacked: {stacked.ToString().ToLower()}
        }},";

            // Add primary y-axis
            scalesConfig += $@"
        y: {{
            type: 'linear',
            display: true,
            position: 'left',
            beginAtZero: true,
            stacked: {stacked.ToString().ToLower()},
            title: {{
                display: true,
                text: 'Primary'
            }}
        }},";

            // Add additional y-axes if needed
            if (multiAxis)
            {
                foreach (string axisId in usedYAxisIds)
                {
                    if (axisId == "y") continue; // Skip the primary axis

                    string position = "right";
                    string title = axisId;
                    string color = null;

                    if (yAxisConfigs.ContainsKey(axisId))
                    {
                        var config = yAxisConfigs[axisId];
                        if (config.ContainsKey("position")) position = config["position"];
                        if (config.ContainsKey("title")) title = config["title"];
                        if (config.ContainsKey("color")) color = config["color"];
                    }

                    scalesConfig += $@"
        {axisId}: {{
            type: 'linear',
            display: true,
            position: '{position}',
            beginAtZero: true,
            grid: {{
                drawOnChartArea: false
            }},
            title: {{
                display: true,
                text: '{title}'
            }}";

                    if (!string.IsNullOrEmpty(color))
                    {
                        scalesConfig += $@",
            ticks: {{
                color: '{color}'
            }}";
                    }

                    scalesConfig += @"
        },";
                }
            }

            scalesConfig += "}";

            // Add the Chart.js initialization script
            html += $@"
<script>
    $(document).ready(function() {{
        var ctx = document.getElementById('{chartId}').getContext('2d');
        var myBarChart = new Chart(ctx, {{
            type: 'bar',
            data: {{
                labels: {JsonConvert.SerializeObject(labels)},
                datasets: [{string.Join(",", datasets)}]
            }},
            options: {{
                indexAxis: '{(horizontal ? "y" : "x")}',
                responsive: true,
                maintainAspectRatio: false,
                {scalesConfig},
                plugins: {{
                    legend: {{
                        display: true,
                        position: 'top'
                    }},
                    title: {{
                        display: true,
                        text: '{chartTitle}'
                    }}
                }}
            }}
        }});
    }});
</script>";

            return html;
        }

        // Updated RenderLineChart method with multiple Y-axes support
        private string RenderLineChart(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the chart
            string chartId = "linechart_" + Guid.NewGuid().ToString("N");

            // Default configuration
            string chartTitle = "Line Chart";
            bool showPoints = true;
            int tension = 0; // 0 = straight lines, higher values = more curved
            bool multiAxis = false;

            // Define default colors to cycle through
            string[] backgroundColors = new string[]
            {
        "rgba(75, 192, 192, 0.2)",
        "rgba(255, 99, 132, 0.2)",
        "rgba(54, 162, 235, 0.2)",
        "rgba(255, 206, 86, 0.2)",
        "rgba(153, 102, 255, 0.2)"
            };

            string[] borderColors = new string[]
            {
        "rgba(75, 192, 192, 1)",
        "rgba(255, 99, 132, 1)",
        "rgba(54, 162, 235, 1)",
        "rgba(255, 206, 86, 1)",
        "rgba(153, 102, 255, 1)"
            };

            // Get series column names
            List<string> seriesColumns = new List<string>();
            if (instructions.ContainsKey("series"))
            {
                string seriesValue = instructions["series"];
                if (seriesValue.StartsWith("[") && seriesValue.EndsWith("]"))
                {
                    // Parse array format
                    seriesValue = seriesValue.Substring(1, seriesValue.Length - 2);
                    seriesColumns = seriesValue.Split(',')
                        .Select(s => s.Trim().Trim('"', '\''))
                        .ToList();
                }
                else
                {
                    seriesColumns.Add(seriesValue);
                }
            }
            else
            {
                // Default to second column
                seriesColumns.Add(data.Columns[1].ColumnName);
            }

            // Check if we need multiple Y-axes
            Dictionary<string, string> seriesYAxisMap = new Dictionary<string, string>();
            if (instructions.ContainsKey("yAxes"))
            {
                multiAxis = true;
                string yAxesValue = instructions["yAxes"];

                if (yAxesValue.StartsWith("{") && yAxesValue.EndsWith("}"))
                {
                    // Remove the outer braces
                    yAxesValue = yAxesValue.Substring(1, yAxesValue.Length - 2);

                    // Split by commas that are not inside quotes
                    List<string> axisMappings = new List<string>();
                    bool inQuotes = false;
                    int startPos = 0;

                    for (int i = 0; i < yAxesValue.Length; i++)
                    {
                        if (yAxesValue[i] == '"' && (i == 0 || yAxesValue[i - 1] != '\\'))
                        {
                            inQuotes = !inQuotes;
                        }
                        else if (yAxesValue[i] == ',' && !inQuotes)
                        {
                            axisMappings.Add(yAxesValue.Substring(startPos, i - startPos).Trim());
                            startPos = i + 1;
                        }
                    }

                    // Add the last part
                    if (startPos < yAxesValue.Length)
                    {
                        axisMappings.Add(yAxesValue.Substring(startPos).Trim());
                    }

                    // Process each mapping
                    foreach (string mapping in axisMappings)
                    {
                        int colonPos = mapping.IndexOf(':');
                        if (colonPos > 0)
                        {
                            string seriesName = mapping.Substring(0, colonPos).Trim().Trim('"');
                            string axisId = mapping.Substring(colonPos + 1).Trim().Trim('"');
                            seriesYAxisMap[seriesName] = axisId;
                        }
                    }
                }
            }

            // Get legends (defaults to first column for labels on x-axis)
            string legendsColumn = data.Columns[0].ColumnName;
            if (instructions.ContainsKey("legends"))
            {
                string legendsValue = instructions["legends"];
                if (legendsValue.StartsWith("[") && legendsValue.EndsWith("]"))
                {
                    // Multiple legends not supported for x-axis, take the first one
                    legendsValue = legendsValue.Substring(1, legendsValue.Length - 2);
                    string firstLegend = legendsValue.Split(',')
                        .Select(s => s.Trim().Trim('"', '\''))
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(firstLegend))
                    {
                        legendsColumn = firstLegend;
                    }
                }
                else
                {
                    legendsColumn = legendsValue;
                }
            }

            // Get legend column index
            int legendsColumnIndex = 0;
            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (data.Columns[i].ColumnName.Equals(legendsColumn, StringComparison.OrdinalIgnoreCase))
                {
                    legendsColumnIndex = i;
                    break;
                }
            }

            // Dictionary to store Y-axis configuration
            Dictionary<string, Dictionary<string, string>> yAxisConfigs = new Dictionary<string, Dictionary<string, string>>();

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
                        chartTitle = formattingStr.Substring(start, end - start).Trim();
                        if (chartTitle.StartsWith("\"") && chartTitle.EndsWith("\""))
                            chartTitle = chartTitle.Substring(1, chartTitle.Length - 2);
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
                        bool.TryParse(showPointsStr, out showPoints);
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
                        int.TryParse(tensionStr, out tension);
                    }
                }

                // Parse Y-axis configurations for multiple axes
                if (multiAxis && formattingStr.Contains("yAxis:"))
                {
                    int yAxisStart = formattingStr.IndexOf("yAxis:") + 6;
                    int yAxisEnd = FindMatchingClosingBrace(formattingStr, yAxisStart);

                    if (yAxisEnd > yAxisStart)
                    {
                        string yAxisStr = formattingStr.Substring(yAxisStart, yAxisEnd - yAxisStart).Trim();

                        // Process each Y-axis configuration
                        int pos = 0;
                        while (pos < yAxisStr.Length)
                        {
                            int idStart = yAxisStr.IndexOf("id:", pos) + 3;
                            if (idStart < 3) break; // No more IDs found

                            int idEnd = yAxisStr.IndexOf(",", idStart);
                            if (idEnd == -1) break;

                            string axisId = yAxisStr.Substring(idStart, idEnd - idStart).Trim().Trim('"');

                            int configStart = yAxisStr.IndexOf("{", idEnd) + 1;
                            int configEnd = FindMatchingClosingBrace(yAxisStr, configStart - 1);

                            if (configEnd > configStart)
                            {
                                string configStr = yAxisStr.Substring(configStart, configEnd - configStart).Trim();
                                Dictionary<string, string> axisConfig = new Dictionary<string, string>();

                                // Parse position
                                int positionStart = configStr.IndexOf("position:") + 9;
                                if (positionStart > 9)
                                {
                                    int positionEnd = configStr.IndexOf(",", positionStart);
                                    if (positionEnd == -1) positionEnd = configStr.Length;
                                    string position = configStr.Substring(positionStart, positionEnd - positionStart).Trim().Trim('"');
                                    axisConfig["position"] = position;
                                }

                                // Parse title
                                int titleStart = configStr.IndexOf("title:") + 6;
                                if (titleStart > 6)
                                {
                                    int titleEnd = configStr.IndexOf(",", titleStart);
                                    if (titleEnd == -1) titleEnd = configStr.Length;
                                    string title = configStr.Substring(titleStart, titleEnd - titleStart).Trim().Trim('"');
                                    axisConfig["title"] = title;
                                }

                                // Parse color
                                int colorStart = configStr.IndexOf("color:") + 6;
                                if (colorStart > 6)
                                {
                                    int colorEnd = configStr.IndexOf(",", colorStart);
                                    if (colorEnd == -1) colorEnd = configStr.Length;
                                    string color = configStr.Substring(colorStart, colorEnd - colorStart).Trim().Trim('"');
                                    axisConfig["color"] = color;
                                }

                                // Store the configuration
                                yAxisConfigs[axisId] = axisConfig;

                                // Move to the next axis configuration
                                pos = configEnd + 1;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }

            // Extract labels for x-axis
            List<string> labels = new List<string>();
            foreach (DataRow row in data.Rows)
            {
                labels.Add(row[legendsColumnIndex].ToString());
            }

            // Prepare datasets for each series
            List<string> datasets = new List<string>();
            HashSet<string> usedYAxisIds = new HashSet<string>();

            for (int i = 0; i < seriesColumns.Count; i++)
            {
                string seriesName = seriesColumns[i];
                int seriesColumnIndex = -1;

                // Find column index by name
                for (int j = 0; j < data.Columns.Count; j++)
                {
                    if (data.Columns[j].ColumnName.Equals(seriesName, StringComparison.OrdinalIgnoreCase))
                    {
                        seriesColumnIndex = j;
                        break;
                    }
                }

                if (seriesColumnIndex == -1) continue; // Skip if column not found

                // Extract values for this series
                List<double> values = new List<double>();
                foreach (DataRow row in data.Rows)
                {
                    values.Add(Convert.ToDouble(row[seriesColumnIndex]));
                }

                // Use current series color from the palette
                string backgroundColor = backgroundColors[i % backgroundColors.Length];
                string borderColor = borderColors[i % borderColors.Length];

                // Custom colors can be added from formatting
                if (instructions.ContainsKey("formatting"))
                {
                    string formattingStr = instructions["formatting"];

                    if (formattingStr.Contains($"backgroundColor{i}:"))
                    {
                        int start = formattingStr.IndexOf($"backgroundColor{i}:") + 15 + i.ToString().Length;
                        int end = formattingStr.IndexOf(",", start);
                        if (end == -1) end = formattingStr.IndexOf("}", start);
                        if (end > start)
                        {
                            backgroundColor = formattingStr.Substring(start, end - start).Trim();
                            if (backgroundColor.StartsWith("\"") && backgroundColor.EndsWith("\""))
                                backgroundColor = backgroundColor.Substring(1, backgroundColor.Length - 2);
                        }
                    }

                    if (formattingStr.Contains($"borderColor{i}:"))
                    {
                        int start = formattingStr.IndexOf($"borderColor{i}:") + 12 + i.ToString().Length;
                        int end = formattingStr.IndexOf(",", start);
                        if (end == -1) end = formattingStr.IndexOf("}", start);
                        if (end > start)
                        {
                            borderColor = formattingStr.Substring(start, end - start).Trim();
                            if (borderColor.StartsWith("\"") && borderColor.EndsWith("\""))
                                borderColor = borderColor.Substring(1, borderColor.Length - 2);
                        }
                    }
                }

                // Determine Y-axis for this series
                string yAxisId = "y";
                if (multiAxis && seriesYAxisMap.ContainsKey(seriesName))
                {
                    yAxisId = seriesYAxisMap[seriesName];
                    usedYAxisIds.Add(yAxisId);
                }

                // Create dataset JSON for this series
                string dataset = $@"{{
            label: '{seriesName}',
            data: {JsonConvert.SerializeObject(values)},
            backgroundColor: '{backgroundColor}',
            borderColor: '{borderColor}',
            borderWidth: 1,
            fill: false,
            pointRadius: {(showPoints ? "3" : "0")},
            tension: {tension / 100.0},
            yAxisID: '{yAxisId}'
        }}";

                datasets.Add(dataset);
            }

            // Create the HTML container for the chart
            string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";

            // Build the scales configuration for Chart.js
            string scalesConfig = "scales: {";

            // Add x-axis configuration
            scalesConfig += @"
        x: {
            display: true
        },";

            // Add primary y-axis
            scalesConfig += @"
        y: {
            type: 'linear',
            display: true,
            position: 'left',
            beginAtZero: true,
            title: {
                display: true,
                text: 'Primary'
            }
        },";

            // Add additional y-axes if needed
            if (multiAxis)
            {
                foreach (string axisId in usedYAxisIds)
                {
                    if (axisId == "y") continue; // Skip the primary axis

                    string position = "right";
                    string title = axisId;
                    string color = null;

                    if (yAxisConfigs.ContainsKey(axisId))
                    {
                        var config = yAxisConfigs[axisId];
                        if (config.ContainsKey("position")) position = config["position"];
                        if (config.ContainsKey("title")) title = config["title"];
                        if (config.ContainsKey("color")) color = config["color"];
                    }

                    scalesConfig += $@"
        {axisId}: {{
            type: 'linear',
            display: true,
            position: '{position}',
            beginAtZero: true,
            grid: {{
                drawOnChartArea: false
            }},
            title: {{
                display: true,
                text: '{title}'
            }}";

                    if (!string.IsNullOrEmpty(color))
                    {
                        scalesConfig += $@",
            ticks: {{
                color: '{color}'
            }}";
                    }

                    scalesConfig += @"
        },";
                }
            }

            scalesConfig += "}";

            // Add the Chart.js initialization script
            html += $@"
<script>
    $(document).ready(function() {{
        var ctx = document.getElementById('{chartId}').getContext('2d');
        var myLineChart = new Chart(ctx, {{
            type: 'line',
            data: {{
                labels: {JsonConvert.SerializeObject(labels)},
                datasets: [{string.Join(",", datasets)}]
            }},
            options: {{
                responsive: true,
                maintainAspectRatio: false,
                {scalesConfig},
                plugins: {{
                    legend: {{
                        display: true,
                        position: 'top'
                    }},
                    title: {{
                        display: true,
                        text: '{chartTitle}'
                    }}
                }}
            }}
        }});
    }});
</script>";

            return html;
        }

        // Helper method to find matching closing brace
        private int FindMatchingClosingBrace(string text, int openBraceIndex)
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
    }
}