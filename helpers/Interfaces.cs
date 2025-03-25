using System.Collections.Generic;
using System.Data;

namespace WebApplication6.Helpers
{
    public interface IDataService
    {
        DataTable ExecuteQuery(string query);
    }

    public interface ITemplateProcessor
    {
        string ProcessTemplate(string templateContent, IDataService dataService);
    }

    public interface IChartRenderer
    {
        string RenderChart(DataTable data, Dictionary<string, string> instructions);
        string RenderDataTable(DataTable data, Dictionary<string, string> instructions);
        string RenderBarChart(DataTable data, Dictionary<string, string> instructions);
        string RenderLineChart(DataTable data, Dictionary<string, string> instructions);
        string RenderPieChart(DataTable data, Dictionary<string, string> instructions);
    }

    public interface IInstructionParser
    {
        Dictionary<string, string> ParseInstructions(string instructionContent);
    }
}