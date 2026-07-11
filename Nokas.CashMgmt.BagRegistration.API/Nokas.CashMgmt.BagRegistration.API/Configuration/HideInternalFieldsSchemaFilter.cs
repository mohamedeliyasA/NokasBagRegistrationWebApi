

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Configuration
{
    /// <summary>
    /// Hides server-controlled fields from BagRegistration in Swagger.
    /// </summary>
    public sealed class HideInternalFieldsSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(Models.BagRegistration) && schema?.Properties != null)
            {
                // Remove using the JSON names that appear in the OpenAPI schema
                schema.Properties.Remove("actionFlag");
                schema.Properties.Remove("registeredBy");
            }
        }
    }
}
