using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using NewsPortal.Api.DTOs;

namespace NewsPortal.Api.Documentation;

// Describe shared validation and authorization responses without attaching
// authentication requirements to public endpoints.
public class ResponseDocumentation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var action = context.MethodInfo.DeclaringType!.Name + "." + context.MethodInfo.Name;
        var responseType = action switch {
            "AdminController.Dashboard" => typeof(DashboardDto),
            "AdminController.Settings" or "PublicSettingsController.Get" => typeof(List<SettingDto>),
            "MediaController.List" => typeof(ItemsPageDto<MediaItemDto>),
            "MediaController.Upload" => typeof(MediaCreatedDto),
            "CommentsController.Create" => typeof(CommentCreatedDto),
            "EditorialController.Revisions" => typeof(ItemsPageDto<RevisionDto>),
            "ReaderController.Reactions" => typeof(List<ReactionCountDto>),
            "ReaderController.Notifications" => typeof(ItemsPageDto<NotificationDto>),
            "NewsletterController.Me" => typeof(SubscriptionStateDto),
            "NewsletterController.Subscribers" => typeof(ItemsPageDto<SubscriberDto>),
            "PollsController.List" => typeof(ItemsPageDto<PollDto>),
            "CategoriesController.Create" or "TagsController.Create" or "PollsController.Create" => typeof(CreatedResourceDto),
            _ => null
        };
        var noContent = context.ApiDescription.HttpMethod is "DELETE" or "PATCH" ||
            (context.ApiDescription.HttpMethod == "PUT" && action != "AuthController.Update") ||
            action is "EditorialController.Review" or "ReaderController.Save" or "ReaderController.ViewArticle" or "AuthController.Logout";
        var creates = context.ApiDescription.HttpMethod == "POST" && !noContent && action != "AuthController.Login";
        var success = noContent ? "204" : creates ? "201" : "200";
        if (success != "200") operation.Responses.Remove("200");
        if (responseType != null) operation.Responses[success] = new() {
            Description = creates ? "Resource created." : "Successful response.",
            Content = new Dictionary<string, OpenApiMediaType> {
                ["application/json"] = new() { Schema = context.SchemaGenerator.GenerateSchema(responseType, context.SchemaRepository) }
            }
        };
        else operation.Responses.TryAdd(success, new() { Description = noContent ? "Completed; no response body." : "Resource created." });
        if (action == "NewsletterController.Subscribe") operation.Responses.TryAdd("204", new() { Description = "Already subscribed." });
        if (context.ApiDescription.HttpMethod is "POST" or "PUT" or "DELETE")
            operation.Responses.TryAdd("409", new() { Description = "Duplicate value, stale RowVersion or resource still in use." });
        operation.Summary ??= context.MethodInfo.Name + " — " + context.MethodInfo.DeclaringType.Name.Replace("Controller", "");
        var protectedEndpoint = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
        operation.Security = protectedEndpoint
            ? [new() { [new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] }]
            : [];
        void Error(string code, string description, Type? type = null)
        {
            operation.Responses.TryAdd(code, new() {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType> {
                    ["application/problem+json"] = new() { Schema = context.SchemaGenerator.GenerateSchema(type ?? typeof(ProblemDetails), context.SchemaRepository) }
                }
            });
        }
        Error("400", "Invalid request or validation failure.", typeof(ValidationProblemDetails));
        if (protectedEndpoint) {
            operation.Responses.TryAdd("401", new() { Description = "Missing, expired or revoked bearer token." });
            operation.Responses.TryAdd("403", new() { Description = "The authenticated account does not have permission." });
        }
        if (context.ApiDescription.ParameterDescriptions.Any(p => p.Source?.Id == "Path"))
            operation.Responses.TryAdd("404", new() { Description = "Resource not found or not publicly visible." });
        Error("500", "Unexpected server error; details are recorded in server logs.");
    }
}
