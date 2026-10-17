using Leoz_25.Infra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

internal class Program
{
	private static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		// appsettings.json holds the LOCAL Windows-authentication connection only; production overrides it with the environment variable ConnectionStrings__DataConnection
		if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DataConnection")))
			throw new InvalidOperationException("ConnectionStrings:DataConnection is not configured. Set it with 'dotnet user-secrets' (Development) or the environment variable 'ConnectionStrings__DataConnection'.");
        // Set max request size (10 MB)
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
        });
        // Add services to the container.
        // PWA offline sync : a replayed offline change (header X-Leoz-Operation-Id) is saved only once (Infra/OfflineOperations.cs, table from Infra/Sql/009)
        builder.Services.AddSingleton<IOfflineOperationStore>(new SqlOfflineOperationStore(builder.Configuration.GetConnectionString("DataConnection")!));

        builder.Services.AddControllersWithViews(options => options.Filters.Add<OfflineOperationFilter>()).AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);

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

		// Session in SQL Server distributed cache : survives App Pool recycle / restart / re-publish
		builder.Services.AddDistributedSqlServerSession(builder.Configuration);

		// Cookie authentication ("Remember Me") : the cookie carries only the user id + a security stamp, encrypted with Data Protection (keys in SQL).
		// The session stays the source of truth; BaseController rebuilds it from this cookie when the session has expired.
		builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
			.AddCookie(options =>
			{
				options.Cookie.Name = ".Leoz.Auth";
				options.Cookie.HttpOnly = true;
				options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
				options.Cookie.SameSite = SameSiteMode.Lax;
				options.Cookie.IsEssential = true;
				options.ExpireTimeSpan = TimeSpan.FromDays(14);
				options.SlidingExpiration = true;
				options.LoginPath = "/Admin/Home/Account";
				options.LogoutPath = "/Admin/Home/Logout";
				options.AccessDeniedPath = "/Admin/Home/Account";
			});

		builder.Services.AddDbContext<DataContext>(db => db.UseSqlServer(builder.Configuration.GetConnectionString("DataConnection")), ServiceLifetime.Scoped);

		builder.Services.AddScoped<IRepositoryWrapper, RepositoryWrapper>();

		// Project Detail module permissions (Permissions:Mode = Legacy | Shadow | Dynamic)
		builder.Services.AddMemoryCache();
		builder.Services.AddScoped<IPermissionService, PermissionService>();

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

		app.UseAuthentication();
		app.UseAuthorization();

		app.UseSession();

		app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

		app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

		app.Run();
	}
}