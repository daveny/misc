using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace WebApplication6.Helpers
{
    public static class ChartHelpers
    {
        public static List<string> ParseArray(string arrayValue)
        {
            if (string.IsNullOrEmpty(arrayValue))
                return new List<string>();

            if (arrayValue.StartsWith("[") && arrayValue.EndsWith("]"))
            {
                // Parse array format
                arrayValue = arrayValue.Substring(1, arrayValue.Length - 2);
                return arrayValue.Split(',')
                    .Select(s => s.Trim().Trim('"', '\''))
                    .ToList();
            }
            else
            {
                return new List<string> { arrayValue };
            }
        }

        public static List<string> GetUniqueOrderedLabels(DataTable data, int legendsColumnIndex)
        {
            // Use OrderBy to ensure consistent ordering and prevent duplicates
            return data.AsEnumerable()
                .Select(row => row[legendsColumnIndex].ToString())
                .Distinct()
                .OrderBy(label => label)
                .ToList();
        }

        public static Dictionary<string, int> GetColumnIndices(DataTable data, List<string> columns)
        {
            var indices = new Dictionary<string, int>();

            foreach (string column in columns)
            {
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    if (data.Columns[i].ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase))
                    {
                        indices[column] = i;
                        break;
                    }
                }
            }

            return indices;
        }

        public static string GetDefaultSortingScript(string sortBy, string sortDirection, string legendsColumn)
        {
            if (string.IsNullOrEmpty(sortBy))
                return string.Empty;

            return $@"
        // Sort data based on '{sortBy}' in '{sortDirection}' order
        (function() {{
            // Determine if sorting by labels (x-axis values) or by a dataset value
            var sortByLabels = '{sortBy}' === '{legendsColumn}';
            var sortDatasetIndex = 0;
            
            if (!sortByLabels) {{
                // Find dataset index for sorting by data values
                var sortByColumn = '{sortBy}';
                
                // Match the sort column with a dataset
                for (var i = 0; i < chartData.datasets.length; i++) {{
                    if (chartData.datasets[i].label.includes(sortByColumn)) {{
                        sortDatasetIndex = i;
                        break;
                    }}
                }}
            }}
            
            // Create pairs of [label, value] for sorting
            var pairs = [];
            for (var i = 0; i < chartData.labels.length; i++) {{
                pairs.push({{
                    label: chartData.labels[i],
                    value: sortByLabels ? chartData.labels[i] : chartData.datasets[sortDatasetIndex].data[i]
                }});
            }}
            
            // Sort the pairs
            pairs.sort(function(a, b) {{
                if (sortByLabels) {{
                    // Try to detect if labels are years or dates
                    var aIsNumber = !isNaN(Number(a.value));
                    var bIsNumber = !isNaN(Number(b.value));
                    
                    if (aIsNumber && bIsNumber) {{
                        // For numeric labels like years, sort numerically
                        return '{sortDirection}' === 'asc' ? 
                            Number(a.value) - Number(b.value) : 
                            Number(b.value) - Number(a.value);
                    }} else {{
                        // For text, sort alphabetically
                        return '{sortDirection}' === 'asc' ? 
                            a.value.localeCompare(b.value) : 
                            b.value.localeCompare(a.value);
                    }}
                }} else {{
                    // For numeric values, sort by the dataset value
                    return '{sortDirection}' === 'asc' ? a.value - b.value : b.value - a.value;
                }}
            }});
            
            // Create new sorted arrays
            var sortedLabels = [];
            var sortedDatasets = [];
            
            // Initialize sorted datasets with empty arrays
            for (var i = 0; i < chartData.datasets.length; i++) {{
                sortedDatasets[i] = [];
            }}
            
            // Reorder all data based on the sorted pairs
            for (var i = 0; i < pairs.length; i++) {{
                sortedLabels.push(pairs[i].label);
                
                // Find original index of this label
                var originalIndex = chartData.labels.indexOf(pairs[i].label);
                
                // Add values from all datasets
                for (var j = 0; j < chartData.datasets.length; j++) {{
                    sortedDatasets[j].push(chartData.datasets[j].data[originalIndex]);
                }}
            }}
            
            // Replace original data with sorted data
            chartData.labels = sortedLabels;
            for (var i = 0; i < chartData.datasets.length; i++) {{
                chartData.datasets[i].data = sortedDatasets[i];
            }}
        }})();
        ";
        }

        public static string GetPieSortingScript(string sortBy, string sortDirection, string legendsColumn)
        {
            if (string.IsNullOrEmpty(sortBy))
                return string.Empty;

            return $@"
        // Sort data based on '{sortBy}' in '{sortDirection}' order
        (function() {{
            // Determine if sorting by labels (pie slice categories) or by values
            var sortByLabels = '{sortBy}' === '{legendsColumn}';
            
            // Create pairs of [label, value, backgroundColor, borderColor] for sorting
            var pairs = [];
            for (var i = 0; i < chartData.labels.length; i++) {{
                pairs.push({{
                    label: chartData.labels[i],
                    value: chartData.datasets[0].data[i],
                    backgroundColor: chartData.datasets[0].backgroundColor[i],
                    borderColor: chartData.datasets[0].borderColor[i]
                }});
            }}
            
            // Sort the pairs
            pairs.sort(function(a, b) {{
                if (sortByLabels) {{
                    // Try to detect if labels are years or dates
                    var aIsNumber = !isNaN(Number(a.label));
                    var bIsNumber = !isNaN(Number(b.label));
                    
                    if (aIsNumber && bIsNumber) {{
                        // For numeric labels like years, sort numerically
                        return '{sortDirection}' === 'asc' ? 
                            Number(a.label) - Number(b.label) : 
                            Number(b.label) - Number(a.label);
                    }} else {{
                        // For text, sort alphabetically
                        return '{sortDirection}' === 'asc' ? 
                            a.label.localeCompare(b.label) : 
                            b.label.localeCompare(a.label);
                    }}
                }} else {{
                    // For numeric values, sort by the slice value
                    return '{sortDirection}' === 'asc' ? a.value - b.value : b.value - a.value;
                }}
            }});
            
            // Create new sorted arrays
            var sortedLabels = [];
            var sortedValues = [];
            var sortedBackgroundColors = [];
            var sortedBorderColors = [];
            
            // Reorder all data based on the sorted pairs
            for (var i = 0; i < pairs.length; i++) {{
                sortedLabels.push(pairs[i].label);
                sortedValues.push(pairs[i].value);
                sortedBackgroundColors.push(pairs[i].backgroundColor);
                sortedBorderColors.push(pairs[i].borderColor);
            }}
            
            // Replace original data with sorted data
            chartData.labels = sortedLabels;
            chartData.datasets[0].data = sortedValues;
            chartData.datasets[0].backgroundColor = sortedBackgroundColors;
            chartData.datasets[0].borderColor = sortedBorderColors;
        }})();
        ";
        }
    }
}