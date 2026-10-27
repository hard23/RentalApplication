using FluentValidation;
using HB.RentalApplication.Api.Contracts;
using HB.RentalApplication.Api.Errors;
using HB.RentalApplication.Application.Commands.CreateApplication;
using HB.RentalApplication.Infrastructure;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Infrastructure persistence layer
builder.Services.AddInfrastructurePersistence(builder.Configuration);

// MediatR handlers from the Application layer
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreateApplicationCommand>());

// FluentValidation validators from the Application layer (run via MediatR pipeline or explicitly)
builder.Services.AddValidatorsFromAssemblyContaining<CreateApplicationCommandValidator>();
// API-level request validators (live in the API assembly)
builder.Services.AddScoped<IValidator<CreateApplicationRequest>, CreateApplicationRequestValidator>();
builder.Services.AddScoped<IValidator<TransitionApplicationRequest>, TransitionApplicationRequestValidator>();
builder.Services.AddScoped<IValidator<GetApplicationsRequest>, GetApplicationsRequestValidator>();

// Consistent ProblemDetails responses and exception mapping (400/404/409/500)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program { }
