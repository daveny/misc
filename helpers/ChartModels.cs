using System.Collections.Generic;

namespace WebApplication6.Helpers
{
    public class ChartOptions
    {
        public string ChartTitle { get; set; } = "Chart";
        public string SortBy { get; set; }
        public string SortDirection { get; set; } = "asc";
        public FormatOptions FormatOptions { get; set; } = new FormatOptions();
    }

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

    public class BarChartOptions : ChartOptions
    {
        public int BorderWidth { get; set; } = 1;
        public bool Horizontal { get; set; } = false;
        public bool Stacked { get; set; } = false;
        public string[] BackgroundColors { get; set; }
        public string[] BorderColors { get; set; }
    }

    public class LineChartOptions : ChartOptions
    {
        public bool ShowPoints { get; set; } = true;
        public int Tension { get; set; } = 0; // 0 = straight lines, higher values = more curved
        public string[] Colors { get; set; }
    }

    public class PieChartOptions : ChartOptions
    {
        public bool ShowLegend { get; set; } = true;
        public bool IsDoughnut { get; set; } = false;
        public bool ShowValues { get; set; } = true;
        public bool ShowPercentages { get; set; } = true;
        public string ValuePosition { get; set; } = "legend"; // 'inside', 'outside', or 'legend'
        public string[] BackgroundColors { get; set; }
        public string[] BorderColors { get; set; }
    }

    public class ChartDataset
    {
        public string Label { get; set; }
        public List<double> Data { get; set; }
        public string BackgroundColor { get; set; }
        public string BorderColor { get; set; }
        public int BorderWidth { get; set; } = 1;
        public double Tension { get; set; } = 0;
        public int PointRadius { get; set; } = 3;
        public bool Fill { get; set; } = false;
    }
}