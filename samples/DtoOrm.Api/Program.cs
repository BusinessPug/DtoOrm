using System.Text.Json.Serialization;
using DtoOrm.Api.Features.Courses;
using DtoOrm.Api.Features.Departments;
using DtoOrm.Api.Features.Enrollments;
using DtoOrm.Api.Features.Offerings;
using DtoOrm.Api.Features.Reports;
using DtoOrm.Api.Features.Students;
using DtoOrm.Api.Features.Teachers;
using DtoOrm.Api.Features.Terms;
using DtoOrm.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MariaDb")
    ?? throw new InvalidOperationException("ConnectionStrings:MariaDb is required.");

builder.Services.AddOrmServices(connectionString);
builder.Services.AddCqrsHandlers();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DtoOrm School API", Version = "v1" });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DtoOrm School API v1");
    c.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapDepartments();
app.MapTeachers();
app.MapStudents();
app.MapCourses();
app.MapTerms();
app.MapOfferings();
app.MapEnrollments();
app.MapReports();

app.Run();

public partial class Program;
