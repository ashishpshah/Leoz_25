using Microsoft.AspNetCore.DataProtection;

namespace Leoz_25.Infra
{
	public static class AppHttpContextAccessor
	{
        private static IHttpContextAccessor _httpContextAccessor;
		private static string _contentRootPath;
		private static string _webRootPath;
		private static IDataProtector _dataProtector;
		private static IConfiguration _iConfig;
		private static IHttpClientFactory _clientFactory;
		private static string _authAPIUrl;
		private static string _connectionString;

		public static void Configure(IHttpContextAccessor httpContextAccessor, IHostEnvironment env_Host, IWebHostEnvironment env_Web, IDataProtectionProvider provider, IConfiguration iConfig, IHttpClientFactory clientFactory)
		{
			_httpContextAccessor = httpContextAccessor;
			_contentRootPath = env_Host.ContentRootPath;
			_webRootPath = env_Web.WebRootPath;
			_dataProtector = provider.CreateProtector("20250409095731001");
			_iConfig = iConfig;
			_clientFactory = clientFactory;

			ConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
			string path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
			configurationBuilder.AddJsonFile(path, optional: false);
			IConfigurationRoot configurationRoot = configurationBuilder.Build();

			_connectionString = configurationRoot.GetSection("ConnectionStrings").GetSection("DataConnection").Value;
		}

		public static string EncrKey => "20250409095731001";
		public static HttpContext AppHttpContext => _httpContextAccessor.HttpContext;
		public static HttpClient AppHttpClient => _clientFactory.CreateClient();
		public static IConfiguration AppConfiguration => _iConfig;

        public static string AppBaseUrl => $"{AppHttpContext.Request.Scheme}://{AppHttpContext.Request.Host}{AppHttpContext.Request.PathBase}";
		public static string ContentRootPath => $"{_contentRootPath}";
		public static string WebRootPath => $"{_webRootPath}";
		public static bool IsLogActive_Info => Convert.ToBoolean(AppHttpContextAccessor.AppConfiguration.GetSection("IsLogActive_Info").Value);
		public static bool IsLogActive_Error => Convert.ToBoolean(AppHttpContextAccessor.AppConfiguration.GetSection("IsLogActive_Error").Value);

		public static bool IsLogged() => Convert.ToInt64(AppHttpContext.Session.GetString(SessionKey.KEY_USER_ID) ?? "0") > 0;
		public static string LoggedUserId() { return AppHttpContext.Session.Keys.Contains(SessionKey.KEY_USER_ID) ? AppHttpContext.Session.GetString(SessionKey.KEY_USER_ID) : ""; }
		public static string LoggedUserType() { return AppHttpContext.Session.Keys.Contains(SessionKey.KEY_USER_TYPE) ? AppHttpContext.Session.GetString(SessionKey.KEY_USER_TYPE) : ""; }

		public static void SetSessionInt(string key, int value) => AppHttpContext.Session.SetInt32(key, value);
		public static long GetSessionInt(string key) => Convert.ToInt64(AppHttpContext.Session.GetString(key) ?? "0");

		public static void SetSession(string key, string value) => AppHttpContext.Session.SetString(key, value);
		public static string GetSession(string key) => AppHttpContext.Session.Keys.Contains(key) ? AppHttpContext.Session.GetString(key) : null;


		public static string Protect(string str) => $"{_dataProtector.Protect(str)}";
		public static string UnProtect(string str) => $"{_dataProtector.Unprotect(str)}";

		//public static HttpClient AppHttpClient_AuthAPI()
		//{
		//	HttpClient client = _clientFactory.CreateClient();
		//	client.BaseAddress = new Uri(_authAPIUrl);
		//	return client;
		//}

	}
}
