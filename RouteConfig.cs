using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace WebApplication6
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "ReportsDEV",
                url: "Report/RenderReportDEV/{templateName}",
                defaults: new { controller = "Report", action = "RenderReportDEV", templateName = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Report",
                url: "Report/{templateName}",
                defaults: new { controller = "Report", action = "RenderReport" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );

            
        }
    }
}
