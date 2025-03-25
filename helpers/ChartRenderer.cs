using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Newtonsoft.Json;

namespace WebApplication6.Helpers
{
    public class ChartRenderer : IChartRenderer
    {
        private readonly TableRenderer _tableRenderer;

        public ChartRenderer()
        {
            _tableRenderer = new TableRenderer();
        }

        public string RenderChart(DataTable data, Dictionary<string, string> instructions)
        {
            string representation = instructions.ContainsKey("representation")
                ? instructions["representation"].ToLower()
                : "table";

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

        public string RenderDataTable(DataTable data, Dictionary<string, string> instructions)
        {
            return _tableRenderer.RenderDataTable(data, instructions);
        }

        public string RenderBarChart(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the chart
            string chartId = "barchart_" + Guid.NewGuid().ToString("N");

            // Parse options from instructions
            var options = FormatParser.ParseBarChartOptions(instructions);

            // Get series column names
            List<string> seriesColumns = new List<string>();
            if (instructions.ContainsKey("series"))
            {
                seriesColumns = ChartHelpers.ParseArray(instructions["series"]);
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
                var legends = ChartHelpers.ParseArray(instructions["legends"]);
                if (legends.Any())
                {
                    legendsColumn = legends.First();
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
            List<string> labels = ChartHelpers.GetUniqueOrderedLabels(data, legendsColumnIndex);

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
                        string backgroundColor = options.BackgroundColors[categoryIndex % options.BackgroundColors.Length];
                        string borderColor = options.BorderColors[categoryIndex % options.BorderColors.Length];

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
                    borderWidth: {options.BorderWidth}
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
                    string backgroundColor = options.BackgroundColors[i % options.BackgroundColors.Length];
                    string borderColor = options.BorderColors[i % options.BorderColors.Length];

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
                borderWidth: {options.BorderWidth}
            }}";

                    datasets.Add(dataset);
                }
            }

            // Create the HTML container for the chart
            string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";

            // Add the Chart.js initialization script with sorting capability
            html += $@"
<script>
    $(document).ready(function() {{
        var ctx = document.getElementById('{chartId}').getContext('2d');
        
        // Original data
        var chartData = {{
            labels: {JsonConvert.SerializeObject(labels)},
            datasets: [{string.Join(",", datasets)}]
        }};
        
        // Apply sorting if specified
        {ChartHelpers.GetDefaultSortingScript(options.SortBy, options.SortDirection, legendsColumn)}
        
        var myBarChart = new Chart(ctx, {{
            type: 'bar',
            data: chartData,
            options: {{
                indexAxis: '{(options.Horizontal ? "y" : "x")}',
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
                        stacked: {options.Stacked.ToString().ToLower()},
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
                        text: '{options.ChartTitle}'
                    }}
                }}
            }}
        }});
    }});
</script>";

            return html;
        }

        public string RenderLineChart(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the chart
            string chartId = "linechart_" + Guid.NewGuid().ToString("N");

            // Parse options from instructions
            var options = FormatParser.ParseLineChartOptions(instructions);

            // Get series column names
            List<string> seriesColumns = new List<string>();
            if (instructions.ContainsKey("series"))
            {
                seriesColumns = ChartHelpers.ParseArray(instructions["series"]);
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
                var legends = ChartHelpers.ParseArray(instructions["legends"]);
                if (legends.Any())
                {
                    legendsColumn = legends.First();
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
                    string color = options.Colors[seriesIndex % options.Colors.Length];
                    string dataset = $@"{{
                label: '{seriesName}',
                data: {JsonConvert.SerializeObject(values)},
                backgroundColor: '{color}',
                borderColor: '{color}',
                fill: false,
                tension: {options.Tension / 100.0},
                pointRadius: {(options.ShowPoints ? "3" : "0")}
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
                        string color = options.Colors[(seriesIndex + categoryIndex) % options.Colors.Length];

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
                    tension: {options.Tension / 100.0},
                    pointRadius: {(options.ShowPoints ? "3" : "0")}
                }}";

                        datasets.Add(dataset);
                        categoryIndex++;
                    }

                    seriesIndex++;
                }
            }

            // Create the HTML container for the chart
            string html = $"<div style='width:100%; height:400px;'><canvas id='{chartId}'></canvas></div>";

            // Add the Chart.js initialization script with sorting capability
            html += $@"
<script>
    $(document).ready(function() {{
        var ctx = document.getElementById('{chartId}').getContext('2d');
        
        // Original data
        var chartData = {{
            labels: {JsonConvert.SerializeObject(labels)},
            datasets: [{string.Join(",", datasets)}]
        }};
        
        // Apply sorting if specified
        {ChartHelpers.GetDefaultSortingScript(options.SortBy, options.SortDirection, legendsColumn)}
        
        var myLineChart = new Chart(ctx, {{
            type: 'line',
            data: chartData,
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
                        text: '{options.ChartTitle}'
                    }}
                }}
            }}
        }});
    }});
</script>";

            return html;
        }

        public string RenderPieChart(DataTable data, Dictionary<string, string> instructions)
        {
            // Create a unique ID for the chart
            string chartId = "piechart_" + Guid.NewGuid().ToString("N");

            // Parse options from instructions
            var options = FormatParser.ParsePieChartOptions(instructions);

            // Get series column name (value column for the pie chart)
            string seriesColumn = "";
            if (instructions.ContainsKey("series"))
            {
                var seriesColumns = ChartHelpers.ParseArray(instructions["series"]);
                if (seriesColumns.Any())
                {
                    seriesColumn = seriesColumns.First();
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
                var legends = ChartHelpers.ParseArray(instructions["legends"]);
                if (legends.Any())
                {
                    legendsColumn = legends.First();
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
                string html = $"<div style='width:100%; display:flex; flex-wrap:wrap; justify-content:center;'>";

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
                        backgroundColorList.Add(options.BackgroundColors[i % options.BackgroundColors.Length]);
                        borderColorList.Add(options.BorderColors[i % options.BorderColors.Length]);
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
        
        // Prepare chart data
        var chartData = {{
            labels: {JsonConvert.SerializeObject(labels)},
            datasets: [{{
                data: {JsonConvert.SerializeObject(values)},
                backgroundColor: {JsonConvert.SerializeObject(backgroundColorList)},
                borderColor: {JsonConvert.SerializeObject(borderColorList)},
                borderWidth: 1
            }}]
        }};
        
        // Apply sorting if specified
        {ChartHelpers.GetPieSortingScript(options.SortBy, options.SortDirection, legendsColumn)}
        
        var myPieChart = new Chart(ctx, {{
            type: '{(options.IsDoughnut ? "doughnut" : "pie")}',
            data: chartData,
            options: {{
                responsive: true,
                maintainAspectRatio: false,
                plugins: {{
                    legend: {{
                        display: {options.ShowLegend.ToString().ToLower()},
                        position: 'right',
                        labels: {{
                            generateLabels: function(chart) {{
                                // Get the default legend items
                                const original = Chart.overrides.pie.plugins.legend.labels.generateLabels;
                                const labels = original.call(this, chart);
                                
                                // Only modify if showing values or percentages in legend
                                if ({(options.ShowValues || options.ShowPercentages).ToString().ToLower()} && '{options.ValuePosition}' === 'legend') {{
                                    // Calculate total for percentages
                                    const total = chart.data.datasets[0].data.reduce((a, b) => a + b, 0);
                                    
                                    // Modify the text to include values/percentages
                                    labels.forEach((label, i) => {{
                                        const value = chart.data.datasets[0].data[i];
                                        const percentage = Math.round((value / total) * 100);
                                        
                                        let newText = label.text;
                                        if ({options.ShowValues.ToString().ToLower()}) {{
                                            newText += ' - ' + value.toLocaleString();
                                        }}
                                        if ({options.ShowPercentages.ToString().ToLower()}) {{
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
            }},
            plugins: [
                {{
                    id: 'insideLabels',
                    afterDraw: function(chart) {{
                        // Only run this plugin for inside position with values or percentages
                        var valuePosition = '{options.ValuePosition}';
                        var showValues = {options.ShowValues.ToString().ToLower()};
                        var showPercentages = {options.ShowPercentages.ToString().ToLower()};
                        
                        if (valuePosition !== 'inside' || (!showValues && !showPercentages)) {{
                            return;
                        }}
                        
                        var ctx = chart.ctx;
                        ctx.save();
                        
                        var total = 0;
                        for (var i = 0; i < chart.data.datasets[0].data.length; i++) {{
                            total += chart.data.datasets[0].data[i];
                        }}
                        
                        for (var i = 0; i < chart.getDatasetMeta(0).data.length; i++) {{
                            var arc = chart.getDatasetMeta(0).data[i];
                            var value = chart.data.datasets[0].data[i];
                            
                            if (value === 0) continue; // Skip zero values
                            
                            // Calculate percentage
                            var percentage = Math.round((value / total) * 100);
                            
                            // Calculate display position
                            var midAngle = (arc.startAngle + arc.endAngle) / 2;
                            var radius = arc.outerRadius * 0.6;
                            
                            // Position based on slice size
                            var slicePercentage = value / total;
                            if (slicePercentage < 0.1) {{ // Small slice
                                radius = arc.outerRadius * 0.5;
                            }} else if (slicePercentage > 0.25) {{ // Large slice
                                radius = arc.outerRadius * 0.7;
                            }}
                            
                            var x = arc.x + Math.cos(midAngle) * radius;
                            var y = arc.y + Math.sin(midAngle) * radius;
                            
                            // Format text
                            var displayText = '';
                            if (showValues) {{
                                displayText += value.toLocaleString();
                            }}
                            if (showPercentages) {{
                                if (displayText) displayText += ' ';
                                displayText += '(' + percentage + '%)';
                            }}
                            
                            // Draw text
                            ctx.font = 'bold 12px Arial';
                            ctx.fillStyle = 'white';
                            ctx.textAlign = 'center';
                            ctx.textBaseline = 'middle';
                            ctx.fillText(displayText, x, y);
                        }}
                        
                        ctx.restore();
                    }}
                }}
            ]
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
                    backgroundColorList.Add(options.BackgroundColors[i % options.BackgroundColors.Length]);
                    borderColorList.Add(options.BorderColors[i % options.BorderColors.Length]);

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

                // Add the Chart.js initialization script with sorting capability
                html += $@"
<script>
    $(document).ready(function() {{
        var ctx = document.getElementById('{chartId}').getContext('2d');
        
        // Prepare chart data
        var chartData = {{
            labels: {JsonConvert.SerializeObject(labels)},
            datasets: [{{
                data: {JsonConvert.SerializeObject(values)},
                backgroundColor: {JsonConvert.SerializeObject(backgroundColorList)},
                borderColor: {JsonConvert.SerializeObject(borderColorList)},
                borderWidth: 1
            }}]
        }};
        
        // Apply sorting if specified
        {ChartHelpers.GetPieSortingScript(options.SortBy, options.SortDirection, legendsColumn)}
        
        var myPieChart = new Chart(ctx, {{
            type: '{(options.IsDoughnut ? "doughnut" : "pie")}',
            data: chartData,
            options: {{
                responsive: true,
                maintainAspectRatio: false,
                plugins: {{
                    legend: {{
                        display: {options.ShowLegend.ToString().ToLower()},
                        position: 'right',
                        labels: {{
                            generateLabels: function(chart) {{
                                // Get the default legend items
                                const original = Chart.overrides.pie.plugins.legend.labels.generateLabels;
                                const labels = original.call(this, chart);
                                
                                // Only modify if showing values or percentages in legend
                                if ({(options.ShowValues || options.ShowPercentages).ToString().ToLower()} && '{options.ValuePosition}' === 'legend') {{
                                    // Calculate total for percentages
                                    const total = chart.data.datasets[0].data.reduce((a, b) => a + b, 0);
                                    
                                    // Modify the text to include values/percentages
                                    labels.forEach((label, i) => {{
                                        const value = chart.data.datasets[0].data[i];
                                        const percentage = Math.round((value / total) * 100);
                                        
                                        let newText = label.text;
                                        if ({options.ShowValues.ToString().ToLower()}) {{
                                            newText += ' - ' + value.toLocaleString();
                                        }}
                                        if ({options.ShowPercentages.ToString().ToLower()}) {{
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
            }},
            plugins: [{{
                id: 'valueLabels',
                afterDraw: function(chart) {{
                    // Only show labels if not legend mode and we're showing values or percentages
                    if ('{options.ValuePosition}' === 'legend' || !({(options.ShowValues || options.ShowPercentages).ToString().ToLower()})) {{
                        return;
                    }}
                    
                    const ctx = chart.ctx;
                    ctx.save();
                    
                    // Get the total for percentage calculations
                    const total = chart.data.datasets[0].data.reduce((a, b) => a + b, 0);
                    
                    chart.getDatasetMeta(0).data.forEach((arc, i) => {{
                        const value = chart.data.datasets[0].data[i];
                        if (value === 0) return; // Skip zero values
                        
                        // Get position based on mode
                        let position = {{x: arc.x, y: arc.y}};
                        if ('{options.ValuePosition}' === 'outside') {{
                            const angle = (arc.startAngle + arc.endAngle) / 2;
                            const radius = arc.outerRadius * 1.2;
                            position.x += Math.cos(angle) * radius;
                            position.y += Math.sin(angle) * radius;
                        }}
                        
                        // Format text based on settings
                        let text = '';
                        if ({options.ShowValues.ToString().ToLower()}) {{
                            text += value.toLocaleString();
                        }}
                        if ({options.ShowPercentages.ToString().ToLower()}) {{
                            const percentage = Math.round((value / total) * 100);
                            if (text) text += ' ';
                            text += '(' + percentage + '%)';
                        }}
                        
                        // Set styling
                        ctx.font = 'bold 12px Arial';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'middle';
                        
                        if ('{options.ValuePosition}' === 'inside') {{
                            ctx.fillStyle = 'white';
                            ctx.fillText(text, position.x, position.y);
                        }} else {{
                            // Add background for better readability
                            const width = ctx.measureText(text).width + 6;
                            const height = 16;
                            ctx.fillStyle = 'rgba(255, 255, 255, 0.7)';
                            ctx.fillRect(position.x - width/2, position.y - height/2, width, height);
                            ctx.fillStyle = '#333';
                            ctx.fillText(text, position.x, position.y);
                        }}
                    }});
                    
                    ctx.restore();
                }}
            }}]
        }});
    }});
</script>";

                return html;
            }
        }
    }
}