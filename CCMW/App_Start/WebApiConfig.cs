using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace CCMW
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Force JSON responses
            config.Formatters.Remove(config.Formatters.XmlFormatter);

            // Enable attribute routing (your [Route] attributes)
            config.MapHttpAttributeRoutes();

            // Convention-based routing
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}