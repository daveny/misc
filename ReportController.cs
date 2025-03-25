using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;
using System.Linq;
using WebApplication6.Helpers;

namespace WebApplication6.Controllers
{
    public class ReportController : Controller
    {
        private readonly IDataService _dataService;
        private readonly ITemplateProcessor _templateProcessor;
        private readonly IChartRenderer _chartRenderer;

        public ReportController()
            : this(new SqlDataService(System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString),
                  new TemplateProcessor(),
                  new ChartRenderer())
        {
        }

        public ReportController(IDataService dataService, ITemplateProcessor templateProcessor, IChartRenderer chartRenderer)
        {
            _dataService = dataService;
            _templateProcessor = templateProcessor;
            _chartRenderer = chartRenderer;
        }

        public ActionResult RenderReport(string templateName)
        {
            try
            {
                // 1. Load the THTML template
                string templatePath = Server.MapPath($"~/Views/Reports/Templates/{templateName}.thtml");
                string templateContent = System.IO.File.ReadAllText(templatePath);

                // 2. Process the template and render the final result
                string renderedContent = _templateProcessor.ProcessTemplate(templateContent, _dataService);

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

        //[Authorize(Roles = "Developer")]
        public ActionResult RenderReportDEV(string templateName)
        {
            try
            {
                // 1. Load the THTML template from the DEV folder
                string templatePath = Server.MapPath($"~/Views/Reports/TemplatesDEV/{templateName}.thtml");
                string templateContent = System.IO.File.ReadAllText(templatePath);

                // 2. Process the template and render the final result
                string renderedContent = _templateProcessor.ProcessTemplate(templateContent, _dataService);

                // 3. Pass the content to the view
                ViewBag.ReportContent = renderedContent;
                ViewBag.ReportName = templateName + " (DEV)";

                // Return the view (reuse the same view as regular reports)
                return View("RenderReport");
            }
            catch (Exception ex)
            {
                // Add logging here
                return Content($"Error: {ex.Message}");
            }
        }

        // Helper method to get all report templates
        public static List<string> GetReportTemplates()
        {
            try
            {
                string templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/Reports/Templates");
                if (!Directory.Exists(templatePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Templates directory does not exist: {templatePath}");
                    return new List<string>();
                }

                // Get all .thtml files, extract their names without extension, and sort alphabetically
                var templates = Directory.GetFiles(templatePath, "*.thtml")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(name => name) // Sort alphabetically
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Found {templates.Count} templates in {templatePath}");
                return templates;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetReportTemplates: {ex.Message}");
                // Return empty list if there's an error
                return new List<string>();
            }
        }

        // Helper method to get DEV report templates (sorted alphabetically)
        public static List<string> GetReportTemplatesDEV()
        {
            try
            {
                string templatePath = System.Web.Hosting.HostingEnvironment.MapPath("~/Views/Reports/TemplatesDEV");
                if (!Directory.Exists(templatePath))
                {
                    System.Diagnostics.Debug.WriteLine($"TemplatesDEV directory does not exist: {templatePath}");
                    return new List<string>();
                }

                // Get all .thtml files, extract their names without extension, and sort alphabetically
                var templates = Directory.GetFiles(templatePath, "*.thtml")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(name => name) // Sort alphabetically
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Found {templates.Count} DEV templates in {templatePath}");
                return templates;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetReportTemplatesDEV: {ex.Message}");
                // Return empty list if there's an error
                return new List<string>();
            }
        }
    }
}