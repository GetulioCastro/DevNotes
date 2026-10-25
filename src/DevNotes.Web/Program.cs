using DevNotes.Web.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDbContext<DevNotesDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DevNotes")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// T-01: em Development, aplica migrations e aplica a seed mínima de demonstração.
if (app.Environment.IsDevelopment())
{
    var appDataPath = Path.Combine(app.Environment.ContentRootPath, "App_Data");
    Directory.CreateDirectory(appDataPath);

    using var escopo = app.Services.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<DevNotesDbContext>();
    db.Database.Migrate();
    await DevNotesSeeder.SeedAsync(db);
}

app.Run();
