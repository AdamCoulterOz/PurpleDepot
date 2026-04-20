using PurpleDepot.Providers.Azure.Options;
using PurpleDepot.Providers.Azure.Storage;
using PurpleDepot.Core.Controller.Data;
using PurpleDepot.Core.Interface.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AppContext = PurpleDepot.Core.Controller.Data.AppContext;

namespace PurpleDepot.Providers.Azure;

public static class Program
{
	private const string OptionsRoot = "PurpleDepot";
	public static void Main()
	{
		var host = new HostBuilder()
			.ConfigureFunctionsWorkerDefaults()
			.ConfigureServices(ConfigureServices)
			.Build();

		host.Run();
	}

	private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
	{
		var config = context.Configuration.GetSection(OptionsRoot).Get<AzureOptions>()
			?? throw new InvalidOperationException($"Missing required configuration section '{OptionsRoot}'.");

		if (config.Development)
		{
			services.AddScoped(typeof(IStorageProvider<>), typeof(MockStorageService<>));
			services.AddDbContext<AppContext>(options => options.UseInMemoryDatabase(config.Database.Name));
		}
		else
		{
			services.AddOptions<StorageOptions>().Bind(context.Configuration.GetSection($"{OptionsRoot}:Storage"));
			services.AddOptions<StorageOptions>().Configure(options => options = config.Storage);
			services.AddTransient(typeof(IStorageProvider<>), typeof(AzureStorageService<>));
			services.AddDbContext<AppContext>(options => options.UseCosmos(config.Database.Connection, databaseName: config.Database.Name));
		}

		services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
	}
}
