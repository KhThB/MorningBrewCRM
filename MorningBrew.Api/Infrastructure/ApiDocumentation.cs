using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MorningBrew.Api.Infrastructure;

public sealed class ApiDocumentation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var authorized = context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any()
            || context.MethodInfo.DeclaringType!.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();
        if (!authorized) operation.Security = [];
        if (context.MethodInfo.Name == "Update" && context.ApiDescription.HttpMethod == "PATCH")
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Description = "Only supplied fields change. Explicit null clears phone, date_of_birth or gender. Send the latest ETag in If-Match.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object, AdditionalPropertiesAllowed = false, MinProperties = 1,
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["full_name"] = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = 120 },
                                ["phone"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, MaxLength = 20 },
                                ["date_of_birth"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "date" },
                                ["gender"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null,
                                    Description = "Male, Female, Other, Undisclosed, or null" }
                            }
                        }
                    }
                }
            };
        operation.Responses ??= new();
        if (context.MethodInfo.Name is "CreateAccount" or "CreateProduct" or "CreateSurvey" or "CreatePromotion"
            || context.ApiDescription.HttpMethod == "POST" && context.MethodInfo.Name is "Supplier" or "Shift" or "Feedback")
        {
            operation.Responses.Remove("200");
            operation.Responses["201"] = new OpenApiResponse
            {
                Description = "Created resource; Location points to its GET endpoint.",
                Headers = new Dictionary<string, IOpenApiHeader> { ["Location"] = new OpenApiHeader { Description = "Resource URL", Schema = new OpenApiSchema { Type = JsonSchemaType.String } } },
                Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = context.SchemaGenerator.GenerateSchema(typeof(Dictionary<string,object>), context.SchemaRepository) } }
            };
        }
        foreach (var code in authorized ? new[] { 400, 401, 403, 404, 409, 429, 503 } : new[] { 400, 401, 429, 503 })
            operation.Responses.TryAdd(code.ToString(), new OpenApiResponse
            {
                Description = ApiContract.Error(code).Error.Status,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = context.SchemaGenerator.GenerateSchema(typeof(ErrorResponse), context.SchemaRepository) }
                }
            });
    }
}
