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
                case "piechart":
                    return RenderPieChart(data, instructions);
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

        // Enhanced RenderPieChart method with data value display
        private string RenderPieChart(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the chart
            string chartId = "piechart_" + Guid.NewGuid().ToString("N");

            // Default configuration
            string chartTitle = "Pie Chart";
            bool showLegend = true;
            bool showLabels = true;
            bool isDoughnut = false;
            bool showValues = true;           // Show data values on the chart by default
            bool showPercentages = true;      // Show percentages on the chart by default
            string valuePosition = "legend";  // Where to display values: 'inside', 'outside', or 'legend'

            // Define default colors to cycle through
            string[] backgroundColors = new string[]
            {
        "rgba(75, 192, 192, 0.7)",
        "rgba(255, 99, 132, 0.7)",
        "rgba(54, 162, 235, 0.7)",
        "rgba(255, 206, 86, 0.7)",
        "rgba(153, 102, 255, 0.7)",
        "rgba(255, 159, 64, 0.7)",
        "rgba(201, 203, 207, 0.7)",
        "rgba(100, 149, 237, 0.7)"
            };

            string[] borderColors = new string[]
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

            // Get series column name (value column for the pie chart)
            string seriesColumn = "";
            if (instructions.ContainsKey("series"))
            {
                string seriesValue = instructions["series"];
                if (seriesValue.StartsWith("[") && seriesValue.EndsWith("]"))
                {
                    // For pie charts, we only use the first series (can't display multiple series in one pie)
                    seriesValue = seriesValue.Substring(1, seriesValue.Length - 2);
                    string firstSeries = seriesValue.Split(',')
                        .Select(s => s.Trim().Trim('"', '\''))
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(firstSeries))
                    {
                        seriesColumn = firstSeries;
                    }
                }
                else
                {
                    seriesColumn = seriesValue;
                }
            }
            else if (data.Columns.Count > 1)
            {
                // Default to second column
                seriesColumn = data.Columns[1].ColumnName;
            }

            // Check if we need to group by a column
            string groupByColumn = null;
            if (instructions.ContainsKey("groupBy"))
            {
                groupByColumn = instructions["groupBy"];
            }

            // Get legends (defaults to first column for labels)
            string legendsColumn = data.Columns[0].ColumnName;
            if (instructions.ContainsKey("legends"))
            {
                string legendsValue = instructions["legends"];
                if (legendsValue.StartsWith("[") && legendsValue.EndsWith("]"))
                {
                    // For pie charts, we only use the first legend
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

                if (formattingStr.Contains("showLegend:"))
                {
                    int start = formattingStr.IndexOf("showLegend:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showLegendStr = formattingStr.Substring(start, end - start).Trim();
                        bool.TryParse(showLegendStr, out showLegend);
                    }
                }

                if (formattingStr.Contains("showLabels:"))
                {
                    int start = formattingStr.IndexOf("showLabels:") + 11;
                    int end = formattingStr.IndexOf(",", start);
                    if (end == -1) end = formattingStr.IndexOf("}", start);
                    if (end > start)
                    {
                        string showLabelsStr = formattingStr.Substring(start, end - start).Trim();
                        bool.TryParse(showLabelsStr, out showLabels);
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
                        bool.TryParse(doughnutStr, out isDoughnut);
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
                        bool.TryParse(showValuesStr, out showValues);
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
                        bool.TryParse(showPercentagesStr, out showPercentages);
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
                            valuePosition = valuePositionStr;
                        }
                    }
                }
            }

            // Get column indices
            int legendsColumnIndex = -1;
            int seriesColumnIndex = -1;
            int groupByColumnIndex = -1;

            // Find legend column index
            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (data.Columns[i].ColumnName.Equals(legendsColumn, StringComparison.OrdinalIgnoreCase))
                {
                    legendsColumnIndex = i;
                    break;
                }
            }

            // Find series column index
            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (data.Columns[i].ColumnName.Equals(seriesColumn, StringComparison.OrdinalIgnoreCase))
                {
                    seriesColumnIndex = i;
                    break;
                }
            }

            // Find groupBy column index if specified
            if (!string.IsNullOrEmpty(groupByColumn))
            {
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    if (data.Columns[i].ColumnName.Equals(groupByColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        groupByColumnIndex = i;
                        break;
                    }
                }
            }

            // Variables to store data for the chart
            List<string> labels = new List<string>();
            List<double> values = new List<double>();
            List<string> backgroundColorList = new List<string>();
            List<string> borderColorList = new List<string>();

            // Different behavior based on whether we're grouping or not
            if (!string.IsNullOrEmpty(groupByColumn) && groupByColumnIndex != -1)
            {
                // We're generating a multi-chart presentation - one pie chart per groupBy value

                // Get unique group values
                List<string> uniqueGroups = data.AsEnumerable()
                    .Select(row => row[groupByColumnIndex].ToString())
                    .Distinct()
                    .OrderBy(g => g)
                    .ToList();

                // Create a container div for all charts
                string html = $"<div style='display: flex; flex-wrap: wrap; justify-content: center;'>";

                // Generate one pie chart for each group
                for (int groupIndex = 0; groupIndex < uniqueGroups.Count; groupIndex++)
                {
                    string groupValue = uniqueGroups[groupIndex];
                    string groupChartId = $"{chartId}_{groupIndex}";

                    // Filter data for this group
                    var groupData = data.AsEnumerable()
                        .Where(row => row[groupByColumnIndex].ToString() == groupValue)
                        .ToList();

                    // Get labels and values for this group
                    labels.Clear();
                    values.Clear();
                    backgroundColorList.Clear();
                    borderColorList.Clear();

                    for (int i = 0; i < groupData.Count; i++)
                    {
                        DataRow row = groupData[i];
                        labels.Add(row[legendsColumnIndex].ToString());
                        values.Add(Convert.ToDouble(row[seriesColumnIndex]));

                        // Use the color palette with cycling
                        backgroundColorList.Add(backgroundColors[i % backgroundColors.Length]);
                        borderColorList.Add(borderColors[i % borderColors.Length]);
                    }

                    // Create chart div for this group
                    html += $"<div style='flex: 1; min-width: 300px; max-width: 500px; margin: 10px;'>";
                    html += $"<h3 style='text-align: center;'>{groupValue}</h3>";
                    html += $"<div style='height: 300px;'><canvas id='{groupChartId}'></canvas></div>";

                    // Add the Chart.js initialization script for this group
                    html += $@"
            <script>
                $(document).ready(function() {{
                    var ctx = document.getElementById('{groupChartId}').getContext('2d');
                    var chart = new Chart(ctx, {{
                        type: '{(isDoughnut ? "doughnut" : "pie")}',
                        data: {{
                            labels: {JsonConvert.SerializeObject(labels)},
                            datasets: [{{
                                data: {JsonConvert.SerializeObject(values)},
                                backgroundColor: {JsonConvert.SerializeObject(backgroundColorList)},
                                borderColor: {JsonConvert.SerializeObject(borderColorList)},
                                borderWidth: 1
                            }}]
                        }},
                        options: {{
                            responsive: true,
                            maintainAspectRatio: false,
                            plugins: {{
                                legend: {{
                                    display: {showLegend.ToString().ToLower()},
                                    position: 'right',
                                    labels: {{
                                        generateLabels: function(chart) {{
                                            // Get the default legend items
                                            const original = Chart.overrides.pie.plugins.legend.labels.generateLabels;
                                            const labels = original.call(this, chart);
                                            
                                            // Only modify if showing values or percentages
                                            if ({(showValues || showPercentages).ToString().ToLower()} && '{valuePosition}' === 'legend') {{
                                                // Calculate total for percentages
                                                const total = chart.data.datasets[0].data.reduce((a, b) => a + b, 0);
                                                
                                                // Modify the text to include values/percentages
                                                labels.forEach((label, i) => {{
                                                    const value = chart.data.datasets[0].data[i];
                                                    const percentage = Math.round((value / total) * 100);
                                                    
                                                    let newText = label.text;
                                                    if ({showValues.ToString().ToLower()}) {{
                                                        newText += ' - ' + value.toLocaleString();
                                                    }}
                                                    if ({showPercentages.ToString().ToLower()}) {{
                                                        newText += ' (' + percentage + '%)';
                                                    }}
                                                    label.text = newText;
                                                }});
                                            }}
                                            
                                            return labels;
                                        }}
                                    }}
                                }},
                                tooltip: {{
                                    callbacks: {{
                                        label: function(context) {{
                                            var label = context.label || '';
                                            var value = context.raw || 0;
                                            var total = context.dataset.data.reduce((a, b) => a + b, 0);
                                            var percentage = Math.round((value / total) * 100);
                                            return label + ': ' + value.toLocaleString() + ' (' + percentage + '%)';
                                        }}
                                    }}
                                }},
                                datalabels: {{
                                    display: {((showValues || showPercentages) && valuePosition != "legend").ToString().ToLower()},
                                    color: 'white',
                                    font: {{
                                        weight: 'bold',
                                        size: 11
                                    }},
                                    textAlign: 'center',
                                    textBaseline: 'middle',
                                    formatter: function(value, context) {{
                                        const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                        const percentage = Math.round((value / total) * 100);
                                        
                                        let result = '';
                                        if ({showValues.ToString().ToLower()}) {{
                                            result += value.toLocaleString();
                                        }}
                                        if ({showPercentages.ToString().ToLower()}) {{
                                            if (result) result += ' ';
                                            result += '(' + percentage + '%)';
                                        }}
                                        return result;
                                    }},
                                    anchor: '{(valuePosition == "inside" ? "center" : "end")}',
                                    align: '{(valuePosition == "inside" ? "center" : "end")}',
                                    offset: {(valuePosition == "inside" ? "0" : "10")}
                                }}
                            }}
                        }}
                    }});
                }});
            </script>";

                    html += "</div>"; // Close the chart div
                }

                html += "</div>"; // Close the container div
                return html;
            }
            else
            {
                // Standard single pie chart
                for (int i = 0; i < data.Rows.Count; i++)
                {
                    DataRow row = data.Rows[i];
                    labels.Add(row[legendsColumnIndex].ToString());
                    values.Add(Convert.ToDouble(row[seriesColumnIndex]));

                    // Use the color palette with cycling
                    backgroundColorList.Add(backgroundColors[i % backgroundColors.Length]);
                    borderColorList.Add(borderColors[i % borderColors.Length]);

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
                                string backgroundColor = formattingStr.Substring(start, end - start).Trim();
                                if (backgroundColor.StartsWith("\"") && backgroundColor.EndsWith("\""))
                                    backgroundColor = backgroundColor.Substring(1, backgroundColor.Length - 2);
                                backgroundColorList[i] = backgroundColor;
                            }
                        }

                        if (formattingStr.Contains($"borderColor{i}:"))
                        {
                            int start = formattingStr.IndexOf($"borderColor{i}:") + 12 + i.ToString().Length;
                            int end = formattingStr.IndexOf(",", start);
                            if (end == -1) end = formattingStr.IndexOf("}", start);
                            if (end > start)
                            {
                                string borderColor = formattingStr.Substring(start, end - start).Trim();
                                if (borderColor.StartsWith("\"") && borderColor.EndsWith("\""))
                                    borderColor = borderColor.Substring(1, borderColor.Length - 2);
                                borderColorList[i] = borderColor;
                            }
                        }
                    }
                }

                // Create the HTML container for the chart
                string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";

                // Add the Chart.js initialization script
                html += $@"
<script>
    $(document).ready(function() {{
        // Load Chart.js DataLabels Plugin if needed
        if (('{valuePosition}' === 'inside' || '{valuePosition}' === 'outside') && 
            {(showValues || showPercentages).ToString().ToLower()} &&
            typeof Chart !== 'undefined' && 
            !Chart.registry.getPlugin('datalabels')) {{
            
            // Define a simplified version of the Chart.js DataLabels plugin
            const ChartDataLabels = {{
                id: 'datalabels',
                afterDatasetsDraw: function(chart) {{
                    const ctx = chart.ctx;
                    
                    chart.data.datasets.forEach((dataset, datasetIndex) => {{
                        const meta = chart.getDatasetMeta(datasetIndex);
                        
                        if (!meta.hidden) {{
                            meta.data.forEach((element, index) => {{
                                const dataValue = dataset.data[index];
                                if (dataValue === 0) return; // Skip zero values
                                
                                // Calculate percentage
                                const total = dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = Math.round((dataValue / total) * 100);
                                
                                // Format display text
                                let displayText = '';
                                if ({showValues.ToString().ToLower()}) {{
                                    displayText += dataValue.toLocaleString();
                                }}
                                if ({showPercentages.ToString().ToLower()}) {{
                                    if (displayText) displayText += ' ';
                                    displayText += '(' + percentage + '%)';
                                }}
                                
                                // Get position
                                let position;
                                if ('{valuePosition}' === 'inside') {{
                                    position = element.getCenterPoint();
                                }} else {{
                                    // Outside - calculate position at the end of the slice
                                    const angle = element.startAngle + (element.endAngle - element.startAngle) / 2;
                                    const radius = element.outerRadius * 1.2;
                                    position = {{
                                        x: element.x + Math.cos(angle) * radius,
                                        y: element.y + Math.sin(angle) * radius
                                    }};
                                }}
                                
                                // Draw text
                                ctx.font = 'bold 12px Arial';
                                ctx.fillStyle = '{valuePosition}' === 'inside' ? 'white' : '#333';
                                ctx.textAlign = 'center';
                                ctx.textBaseline = 'middle';
                                
                                if ('{valuePosition}' === 'outside') {{
                                    // Add background for outside labels
                                    const width = ctx.measureText(displayText).width + 6;
                                    const height = 16;
                                    ctx.fillStyle = 'rgba(255, 255, 255, 0.7)';
                                    ctx.fillRect(position.x - width/2, position.y - height/2, width, height);
                                    ctx.fillStyle = '#333';
                                }}
                                
                                ctx.fillText(displayText, position.x, position.y);
                            }});
                        }}
                    }});
                }}
            }};
            
            // Register plugin
            Chart.register(ChartDataLabels);
        }}
        
        var ctx = document.getElementById('{chartId}').getContext('2d');
        var myPieChart = new Chart(ctx, {{
            type: '{(isDoughnut ? "doughnut" : "pie")}',
            data: {{
                labels: {JsonConvert.SerializeObject(labels)},
                datasets: [{{
                    data: {JsonConvert.SerializeObject(values)},
                    backgroundColor: {JsonConvert.SerializeObject(backgroundColorList)},
                    borderColor: {JsonConvert.SerializeObject(borderColorList)},
                    borderWidth: 1
                }}]
            }},
            options: {{
                responsive: true,
                maintainAspectRatio: false,
                plugins: {{
                    legend: {{
                        display: {showLegend.ToString().ToLower()},
                        position: 'right',
                        labels: {{
                            generateLabels: function(chart) {{
                                // Get the default legend items
                                const original = Chart.overrides.pie.plugins.legend.labels.generateLabels;
                                const labels = original.call(this, chart);
                                
                                // Only modify if showing values or percentages
                                if ({(showValues || showPercentages).ToString().ToLower()} && '{valuePosition}' === 'legend') {{
                                    // Calculate total for percentages
                                    const total = chart.data.datasets[0].data.reduce((a, b) => a + b, 0);
                                    
                                    // Modify the text to include values/percentages
                                    labels.forEach((label, i) => {{
                                        const value = chart.data.datasets[0].data[i];
                                        const percentage = Math.round((value / total) * 100);
                                        
                                        let newText = label.text;
                                        if ({showValues.ToString().ToLower()}) {{
                                            newText += ' - ' + value.toLocaleString();
                                        }}
                                        if ({showPercentages.ToString().ToLower()}) {{
                                            newText += ' (' + percentage + '%)';
                                        }}
                                        label.text = newText;
                                    }});
                                }}
                                
                                return labels;
                            }}
                        }}
                    }},
                    tooltip: {{
                        callbacks: {{
                            label: function(context) {{
                                var label = context.label || '';
                                var value = context.raw || 0;
                                var total = context.dataset.data.reduce((a, b) => a + b, 0);
                                var percentage = Math.round((value / total) * 100);
                                return label + ': ' + value.toLocaleString() + ' (' + percentage + '%)';
                            }}
                        }}
                    }}
                }}
            }}
        }});
    }});
</script>";

                return html;
            }
        }


        // This is a complete implementation of the BarChart method with the fix for duplicate labels

        private string RenderBarChart(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the chart
            string chartId = "barchart_" + Guid.NewGuid().ToString("N");

            // Default configuration
            string chartTitle = "Bar Chart";
            int borderWidth = 1;
            bool horizontal = false;
            bool stacked = false;

            // Define default colors to cycle through
            string[] backgroundColors = new string[]
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

            string[] borderColors = new string[]
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

            // Check if we need to group by a column
            string groupByColumn = null;
            if (instructions.ContainsKey("groupBy"))
            {
                groupByColumn = instructions["groupBy"];
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
            }

            // Get column indices
            int legendsColumnIndex = -1;
            Dictionary<string, int> seriesColumnIndices = new Dictionary<string, int>();
            int groupByColumnIndex = -1;

            // Find legend column index
            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (data.Columns[i].ColumnName.Equals(legendsColumn, StringComparison.OrdinalIgnoreCase))
                {
                    legendsColumnIndex = i;
                    break;
                }
            }

            // Find series column indices
            foreach (string seriesName in seriesColumns)
            {
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    if (data.Columns[i].ColumnName.Equals(seriesName, StringComparison.OrdinalIgnoreCase))
                    {
                        seriesColumnIndices[seriesName] = i;
                        break;
                    }
                }
            }

            // Find groupBy column index if specified
            if (!string.IsNullOrEmpty(groupByColumn))
            {
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    if (data.Columns[i].ColumnName.Equals(groupByColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        groupByColumnIndex = i;
                        break;
                    }
                }
            }

            // Get unique ORDERED labels for x-axis
            List<string> labels = GetUniqueOrderedLabels(data, legendsColumnIndex);

            // Prepare datasets
            List<string> datasets = new List<string>();

            // If groupBy is specified and valid
            if (!string.IsNullOrEmpty(groupByColumn) && groupByColumnIndex != -1)
            {
                // Get unique categories from groupBy column
                List<string> uniqueCategories = data.AsEnumerable()
                    .Select(row => row[groupByColumnIndex].ToString())
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                // For each series
                int seriesIndex = 0;
                foreach (string seriesName in seriesColumns)
                {
                    if (!seriesColumnIndices.ContainsKey(seriesName)) continue;

                    int colIndex = seriesColumnIndices[seriesName];

                    // For each category create a dataset
                    int categoryIndex = 0;
                    foreach (string category in uniqueCategories)
                    {
                        // Extract values for this category
                        Dictionary<string, double> valuesByLegend = new Dictionary<string, double>();

                        // Initialize all legends with zero value
                        foreach (string legend in labels)
                        {
                            valuesByLegend[legend] = 0;
                        }

                        // Fill in actual values
                        foreach (DataRow row in data.Rows)
                        {
                            if (row[groupByColumnIndex].ToString() == category)
                            {
                                string legend = row[legendsColumnIndex].ToString();
                                if (valuesByLegend.ContainsKey(legend))
                                {
                                    valuesByLegend[legend] = Convert.ToDouble(row[colIndex]);
                                }
                            }
                        }

                        // Extract ordered values based on labels
                        List<double> values = new List<double>();
                        foreach (string legend in labels)
                        {
                            values.Add(valuesByLegend[legend]);
                        }

                        // Use colors from the palette with cycling
                        string backgroundColor = backgroundColors[categoryIndex % backgroundColors.Length];
                        string borderColor = borderColors[categoryIndex % borderColors.Length];

                        // Custom colors from formatting
                        if (instructions.ContainsKey("formatting"))
                        {
                            string formattingStr = instructions["formatting"];

                            if (formattingStr.Contains($"backgroundColor{categoryIndex}:"))
                            {
                                int start = formattingStr.IndexOf($"backgroundColor{categoryIndex}:") + 15 + categoryIndex.ToString().Length;
                                int end = formattingStr.IndexOf(",", start);
                                if (end == -1) end = formattingStr.IndexOf("}", start);
                                if (end > start)
                                {
                                    backgroundColor = formattingStr.Substring(start, end - start).Trim();
                                    if (backgroundColor.StartsWith("\"") && backgroundColor.EndsWith("\""))
                                        backgroundColor = backgroundColor.Substring(1, backgroundColor.Length - 2);
                                }
                            }

                            if (formattingStr.Contains($"borderColor{categoryIndex}:"))
                            {
                                int start = formattingStr.IndexOf($"borderColor{categoryIndex}:") + 12 + categoryIndex.ToString().Length;
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

                        // Create dataset label
                        string datasetLabel = seriesColumns.Count > 1
                            ? $"{seriesName} - {category}"
                            : category;

                        // Create dataset JSON
                        string dataset = $@"{{
                    label: '{datasetLabel}',
                    data: {JsonConvert.SerializeObject(values)},
                    backgroundColor: '{backgroundColor}',
                    borderColor: '{borderColor}',
                    borderWidth: {borderWidth}
                }}";

                        datasets.Add(dataset);
                        categoryIndex++;
                    }

                    seriesIndex++;
                }
            }
            else
            {
                // Traditional chart rendering (no groupBy)
                for (int i = 0; i < seriesColumns.Count; i++)
                {
                    string seriesName = seriesColumns[i];
                    if (!seriesColumnIndices.ContainsKey(seriesName)) continue;

                    int colIndex = seriesColumnIndices[seriesName];

                    // Extract values for this series
                    List<double> values = new List<double>();
                    foreach (DataRow row in data.Rows)
                    {
                        values.Add(Convert.ToDouble(row[colIndex]));
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

                    // Create dataset JSON for this series
                    string dataset = $@"{{
                label: '{seriesName}',
                data: {JsonConvert.SerializeObject(values)},
                backgroundColor: '{backgroundColor}',
                borderColor: '{borderColor}',
                borderWidth: {borderWidth}
            }}";

                    datasets.Add(dataset);
                }
            }

            // Create the HTML container for the chart
            string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";

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
                scales: {{
                    x: {{
                        type: 'category',
                        display: true,
                        grid: {{
                            offset: false
                        }},
                        ticks: {{
                            autoSkip: false
                        }}
                    }},
                    y: {{
                        stacked: {stacked.ToString().ToLower()},
                        beginAtZero: true
                    }}
                }},
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

        // Helper method to get unique ordered labels
        private List<string> GetUniqueOrderedLabels(DataTable data, int legendsColumnIndex)
        {
            // Use OrderBy to ensure consistent ordering and prevent duplicates
            return data.AsEnumerable()
                .Select(row => row[legendsColumnIndex].ToString())
                .Distinct()
                .OrderBy(label => label)
                .ToList();
        }

        // Updated RenderLineChart method with multiple Y-axes support
        // Complete implementation of RenderLineChart with groupBy support
        private string RenderLineChart(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the chart
            string chartId = "linechart_" + Guid.NewGuid().ToString("N");

            // Default configuration
            string chartTitle = "Line Chart";
            bool showPoints = true;
            int tension = 0; // 0 = straight lines, higher values = more curved

            // Define default colors to cycle through
            string[] colors = new string[]
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

            // Check if we need to group by a column
            string groupByColumn = null;
            if (instructions.ContainsKey("groupBy"))
            {
                groupByColumn = instructions["groupBy"];
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
            }

            // Get column indices
            int legendsColumnIndex = -1;
            Dictionary<string, int> seriesColumnIndices = new Dictionary<string, int>();
            int groupByColumnIndex = -1;

            // Find legend column index
            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (data.Columns[i].ColumnName.Equals(legendsColumn, StringComparison.OrdinalIgnoreCase))
                {
                    legendsColumnIndex = i;
                    break;
                }
            }

            // Find series column indices
            foreach (string seriesName in seriesColumns)
            {
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    if (data.Columns[i].ColumnName.Equals(seriesName, StringComparison.OrdinalIgnoreCase))
                    {
                        seriesColumnIndices[seriesName] = i;
                        break;
                    }
                }
            }

            // Find groupBy column index if specified
            if (!string.IsNullOrEmpty(groupByColumn))
            {
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    if (data.Columns[i].ColumnName.Equals(groupByColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        groupByColumnIndex = i;
                        break;
                    }
                }
            }

            // Extract labels for x-axis and prepare datasets
            List<string> labels = new List<string>();
            List<string> datasets = new List<string>();

            // Standard chart (no groupBy)
            if (string.IsNullOrEmpty(groupByColumn) || groupByColumnIndex == -1)
            {
                // Extract labels
                foreach (DataRow row in data.Rows)
                {
                    labels.Add(row[legendsColumnIndex].ToString());
                }

                // Create a dataset for each series
                int seriesIndex = 0;
                foreach (string seriesName in seriesColumns)
                {
                    if (!seriesColumnIndices.ContainsKey(seriesName)) continue;

                    int colIndex = seriesColumnIndices[seriesName];
                    List<double> values = new List<double>();

                    foreach (DataRow row in data.Rows)
                    {
                        values.Add(Convert.ToDouble(row[colIndex]));
                    }

                    // Create dataset
                    string color = colors[seriesIndex % colors.Length];
                    string dataset = $@"{{
                label: '{seriesName}',
                data: {JsonConvert.SerializeObject(values)},
                backgroundColor: '{color}',
                borderColor: '{color}',
                fill: false,
                tension: {tension / 100.0},
                pointRadius: {(showPoints ? "3" : "0")}
            }}";

                    datasets.Add(dataset);
                    seriesIndex++;
                }
            }
            // GroupBy chart
            else
            {
                // Get unique legends for x-axis
                HashSet<string> uniqueLegends = new HashSet<string>();
                foreach (DataRow row in data.Rows)
                {
                    uniqueLegends.Add(row[legendsColumnIndex].ToString());
                }
                labels = uniqueLegends.OrderBy(l => l).ToList();

                // Get unique categories from groupBy column
                HashSet<string> uniqueCategories = new HashSet<string>();
                foreach (DataRow row in data.Rows)
                {
                    uniqueCategories.Add(row[groupByColumnIndex].ToString());
                }

                // For each series column
                int seriesIndex = 0;
                foreach (string seriesName in seriesColumns)
                {
                    if (!seriesColumnIndices.ContainsKey(seriesName)) continue;

                    int colIndex = seriesColumnIndices[seriesName];

                    // For each category, create a separate dataset
                    int categoryIndex = 0;
                    foreach (string category in uniqueCategories.OrderBy(c => c))
                    {
                        // Create a mapping of legend to value for this category
                        Dictionary<string, double> valueMap = new Dictionary<string, double>();

                        // Initialize all labels with zero
                        foreach (string label in labels)
                        {
                            valueMap[label] = 0;
                        }

                        // Fill in values from matching rows
                        foreach (DataRow row in data.Rows)
                        {
                            if (row[groupByColumnIndex].ToString() == category)
                            {
                                string legend = row[legendsColumnIndex].ToString();
                                valueMap[legend] = Convert.ToDouble(row[colIndex]);
                            }
                        }

                        // Extract values in label order
                        List<double> values = new List<double>();
                        foreach (string label in labels)
                        {
                            values.Add(valueMap[label]);
                        }

                        // Select color for this dataset
                        string color = colors[(seriesIndex + categoryIndex) % colors.Length];

                        // Create dataset label (combine series and category if multiple series)
                        string datasetLabel = seriesColumns.Count > 1
                            ? $"{seriesName} - {category}"
                            : category;

                        // Create dataset
                        string dataset = $@"{{
                    label: '{datasetLabel}',
                    data: {JsonConvert.SerializeObject(values)},
                    backgroundColor: '{color}',
                    borderColor: '{color}',
                    fill: false,
                    tension: {tension / 100.0},
                    pointRadius: {(showPoints ? "3" : "0")}
                }}";

                        datasets.Add(dataset);
                        categoryIndex++;
                    }

                    seriesIndex++;
                }
            }

            // Create the HTML container for the chart
            string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";

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
                scales: {{
                    x: {{
                        display: true
                    }},
                    y: {{
                        display: true,
                        beginAtZero: true
                    }}
                }},
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