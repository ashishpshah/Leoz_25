using Leoz_25.Infra;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

internal class Program
{
	private static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		// Add services to the container.
		builder.Services.AddControllersWithViews().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);

		builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
		{
			options.SerializerOptions.PropertyNameCaseInsensitive = false;
			options.SerializerOptions.PropertyNamingPolicy = null;
			options.SerializerOptions.WriteIndented = true;
		});

		builder.Services.AddHttpClient();

		builder.Services.AddHttpContextAccessor();

		builder.Services.Configure<RequestLocalizationOptions>(options =>
		{
			var cultureInfo = new CultureInfo("en-IN");
			cultureInfo.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
			cultureInfo.DateTimeFormat.LongDatePattern = "dd/MM/yyyy HH:mm:ss";

			var supportedCultures = new List<CultureInfo> { cultureInfo };

			options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-IN");

			options.DefaultRequestCulture.Culture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
			options.DefaultRequestCulture.Culture.DateTimeFormat.LongDatePattern = "dd/MM/yyyy HH:mm:ss";

			options.SupportedCultures = supportedCultures;
			options.SupportedUICultures = supportedCultures;
		});

		var culture = CultureInfo.CreateSpecificCulture("en-IN");

		var dateformat = new DateTimeFormatInfo { ShortDatePattern = "dd/MM/yyyy", LongDatePattern = "dd/MM/yyyy HH:mm:ss" };

		culture.DateTimeFormat = dateformat;

		var supportedCultures = new[] { culture };

		builder.Services.Configure<RequestLocalizationOptions>(options =>
		{
			options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culture);
			options.SupportedCultures = supportedCultures;
			options.SupportedUICultures = supportedCultures;
		});

		builder.Services.AddSession(options => { options.IdleTimeout = TimeSpan.FromMinutes(30); });

		builder.Services.AddDbContext<DataContext>(db => db.UseSqlServer(builder.Configuration.GetConnectionString("DataConnection")), ServiceLifetime.Singleton);

		builder.Services.AddScoped<IRepositoryWrapper, RepositoryWrapper>();

		var app = builder.Build();

		AppHttpContextAccessor.Configure(((IApplicationBuilder)app).ApplicationServices.GetRequiredService<IHttpContextAccessor>(), ((IApplicationBuilder)app).ApplicationServices.GetRequiredService<IHostEnvironment>(), builder.Environment, ((IApplicationBuilder)app).ApplicationServices.GetRequiredService<IDataProtectionProvider>(), ((IApplicationBuilder)app).ApplicationServices.GetRequiredService<IConfiguration>(), ((IApplicationBuilder)app).ApplicationServices.GetRequiredService<IHttpClientFactory>());

		// Configure the HTTP request pipeline.
		if (!app.Environment.IsDevelopment())
		{
			app.UseExceptionHandler("/Home/Error");
			// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
			app.UseHsts();
		}

		app.UseHttpsRedirection();
		app.UseStaticFiles();

		app.UseRouting();

		app.UseAuthorization();

		app.UseSession();

		app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

		app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

		app.Run();
	}
}