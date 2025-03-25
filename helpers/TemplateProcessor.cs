using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace WebApplication6.Helpers
{
    public class TemplateProcessor : ITemplateProcessor
    {
        private readonly IInstructionParser _instructionParser;
        private readonly IChartRenderer _chartRenderer;

        public TemplateProcessor() : this(new InstructionParser(), new ChartRenderer())
        {
        }

        public TemplateProcessor(IInstructionParser instructionParser, IChartRenderer chartRenderer)
        {
            _instructionParser = instructionParser;
            _chartRenderer = chartRenderer;
        }

        public string ProcessTemplate(string templateContent, IDataService dataService)
        {
            // Remove the template-content wrapper if present
            templateContent = templateContent.Replace("<template-content>", "").Replace("</template-content>", "");

            // Find all tokens in the template using regex
            string pattern = @"\{\{((?:[^{}]|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!)))\}\}";
            MatchCollection matches = Regex.Matches(templateContent, pattern);

            // Process each token
            foreach (Match match in matches)
            {
                string token = match.Value;
                string instructionContent = match.Groups[1].Value.Trim();

                // Parse instructions from the token
                var instructions = _instructionParser.ParseInstructions(instructionContent);

                // Execute the query and get the data
                DataTable data = dataService.ExecuteQuery(instructions["query"]);

                // Render the data according to the specified representation and formatting
                string renderedData = _chartRenderer.RenderChart(data, instructions);

                // Replace the token with the rendered content
                templateContent = templateContent.Replace(token, renderedData);
            }

            return templateContent;
        }
    }

    public class InstructionParser : IInstructionParser
    {
        public Dictionary<string, string> ParseInstructions(string instructionContent)
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
    }
}