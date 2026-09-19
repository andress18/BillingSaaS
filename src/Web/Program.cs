using BillingSaaS.Application;
using BillingSaaS.Infrastructure;
using BillingSaaS.Infrastructure.Data;
using BillingSaaS.ServiceDefaults;
using BillingSaaS.Web;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.AddKeyVaultIfConfigured();
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
await app.InitialiseDatabaseAsync();

if (app.Environment.IsDevelopment())
{
}
else
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(static builder => 
    builder.AllowAnyMethod()
        .AllowAnyHeader()
        .AllowAnyOrigin());

app.UseFileServer();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseExceptionHandler(options => { });

app.Map("/", () => Results.Redirect("/scalar"));

app.MapDefaultEndpoints();
app.MapEndpoints(typeof(Program).Assembly);

app.MapGet("/api/v1/facturas/{id:int}/pdf", async (MediatR.ISender sender, HttpContext httpContext, int id) =>
{
    var result = await sender.Send(new BillingSaaS.Application.Facturas.Queries.GetFacturaPdf.GetFacturaPdfQuery(id));
    httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{result.FileName}\"";
    return Results.File(result.Content, "application/pdf");
}).RequireAuthorization().WithTags("Facturas");

app.MapGet("/api/v1/facturas/{id:int}/xml", async (MediatR.ISender sender, HttpContext httpContext, int id, bool? raw) =>
{
    var result = await sender.Send(new BillingSaaS.Application.Facturas.Queries.GetFacturaXml.GetFacturaXmlQuery(id, raw ?? false));
    httpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{result.FileName}\"";
    return Results.File(result.Content, result.ContentType);
}).RequireAuthorization().WithTags("Facturas");

app.MapGet("/api/Suscripciones/actual", async (MediatR.ISender sender) =>
{
    var response = await sender.Send(new BillingSaaS.Application.Suscripciones.Queries.GetTenantSubscription.GetTenantSubscriptionQuery());
    return Results.Ok(response);
}).RequireAuthorization().WithTags("Suscripciones");

app.MapGet("/api/Suscripciones/planes", async (MediatR.ISender sender) =>
{
    var response = await sender.Send(new BillingSaaS.Application.Suscripciones.Queries.GetPlanes.GetPlanesQuery());
    return Results.Ok(response);
}).WithTags("Suscripciones");

app.MapPost("/api/Suscripciones/renovar", async (MediatR.ISender sender, BillingSaaS.Application.Suscripciones.Commands.RenovarSuscripcion.RenovarSuscripcionCommand command) =>
{
    var response = await sender.Send(command);
    return Results.Ok(response);
}).RequireAuthorization().WithTags("Suscripciones");

app.MapGet("/api/v1/emisores/regimenes-rimpe", BillingSaaS.Web.Endpoints.Emisores.GetRegimenesRimpe)
    .RequireAuthorization()
    .WithTags("Emisores");

app.MapGet("/api/v1/admin/clientes", async (MediatR.ISender sender, [Microsoft.AspNetCore.Mvc.FromQuery] Guid? partnerId) =>
{
    var response = await sender.Send(new BillingSaaS.Application.Tenants.Queries.GetClientesCartera.GetClientesCarteraQuery(partnerId));
    return Results.Ok(response);
}).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute { Roles = BillingSaaS.Domain.Constants.Roles.Administrator }).WithTags("Admin");

app.Run();
