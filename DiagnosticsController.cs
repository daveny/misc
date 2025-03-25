using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace WebApplication6.Controllers
{
    public class DiagnosticsController : Controller
    {
        public ActionResult Index()
        {
            var diagnostics = new Dictionary<string, string>();

            try
            {
                // Basic app info
                diagnostics.Add("Application Path", Server.MapPath("~/"));
                diagnostics.Add("Temporary Path", Path.GetTempPath());
                diagnostics.Add("Machine Name", Environment.MachineName);
                diagnostics.Add("OS Version", Environment.OSVersion.ToString());
                diagnostics.Add("ASP.NET Version", Environment.Version.ToString());

                // Check template folders
                string templatesPath = Server.MapPath("~/Views/Reports/Templates");
                string templatesDevPath = Server.MapPath("~/Views/Reports/TemplatesDev");

                diagnostics.Add("Templates Path", templatesPath);
                diagnostics.Add("Templates Path Exists", Directory.Exists(templatesPath).ToString());

                diagnostics.Add("Templates Dev Path", templatesDevPath);
                diagnostics.Add("Templates Dev Path Exists", Directory.Exists(templatesDevPath).ToString());

                // Template files
                if (Directory.Exists(templatesPath))
                {
                    var templateFiles = Directory.GetFiles(templatesPath, "*.thtml")
                                              .Select(Path.GetFileName);
                    diagnostics.Add("Template Files", string.Join(", ", templateFiles));
                }

                // Check database connection
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
                diagnostics.Add("Connection String", MaskConnectionString(connectionString));

                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        diagnostics.Add("Database Connection", "Success");

                        // Get database info
                        diagnostics.Add("Database Server", conn.DataSource);
                        diagnostics.Add("Database Name", conn.Database);
                        diagnostics.Add("Database Version", conn.ServerVersion);

                        // Test a sample query
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT @@VERSION";
                            var version = cmd.ExecuteScalar()?.ToString();
                            diagnostics.Add("SQL Version Full", version ?? "NULL");
                        }
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.Add("Database Connection", "Failed");
                    diagnostics.Add("Database Error", ex.Message);
                    diagnostics.Add("Database Stack", ex.StackTrace);
                }

                // Check JS/CSS resources
                string jsPath = Server.MapPath("~/Content/js");
                string cssPath = Server.MapPath("~/Content/css");

                if (Directory.Exists(jsPath))
                {
                    var jsFiles = Directory.GetFiles(jsPath, "*.js")
                                      .Select(Path.GetFileName);
                    diagnostics.Add("JS Files", string.Join(", ", jsFiles));
                }

                if (Directory.Exists(cssPath))
                {
                    var cssFiles = Directory.GetFiles(cssPath, "*.css")
                                       .Select(Path.GetFileName);
                    diagnostics.Add("CSS Files", string.Join(", ", cssFiles));
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add("Diagnostics Error", ex.Message);
                diagnostics.Add("Diagnostics Stack", ex.StackTrace);
            }

            return View(diagnostics);
        }

        public ActionResult FileSystem()
        {
            var root = Server.MapPath("~/");
            var model = new FileSystemViewModel
            {
                Path = root,
                Directories = Directory.GetDirectories(root).Select(Path.GetFileName).ToList(),
                Files = Directory.GetFiles(root).Select(Path.GetFileName).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public ActionResult TestConnection()
        {
            try
            {
                var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
                var result = new Dictionary<string, string>();

                result.Add("ConnectionString", MaskConnectionString(connectionString));

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    result.Add("Status", "Connected");
                    result.Add("Server", conn.DataSource);
                    result.Add("Database", conn.Database);

                    // Try a simple query
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT @@VERSION";
                        var version = cmd.ExecuteScalar()?.ToString();
                        result.Add("Version", version ?? "NULL");
                    }
                }

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Status = "Error",
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                }, JsonRequestBehavior.AllowGet);
            }
        }

        private string MaskConnectionString(string connectionString)
        {
            // Mask password for security
            return System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                "Password=.*?;",
                "Password=********;",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }

    public class FileSystemViewModel
    {
        public string Path { get; set; }
        public List<string> Directories { get; set; }
        public List<string> Files { get; set; }
    }
}