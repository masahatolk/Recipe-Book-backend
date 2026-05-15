using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RecipeBook.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using RecipeBook.Api.Data;
using RecipeBook.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RecipeBookDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("RecipeBook")));

builder.Services.AddScoped<RecipeService>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});



builder.Services
    .AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RecipeBookDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.ContentType = "application/json";

        if (error is ProductDeletionBlockedException blocked)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new ProductDeletionBlockedResponse(
                "Удаление недоступно. Продукт используется в блюдах",
                blocked.DishNames));
            return;
        }

        if (error is ArgumentException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse(ex.Message));
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse(error?.Message ?? "Internal server error"));
    });
});

app.MapControllers();

app.Run();

public partial class Program { }