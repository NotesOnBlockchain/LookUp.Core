using LookUp.Config;
using LookUp.Core.Rpc;
using LookUp.Helpers;
using LookUp.Logger;
using LookUp.Scanner;
using LookUp.Scanner.Cache;
using LookUp.Scanner.DataBase;
using LookUp.Scanner.Helpers;
using LookUp.Scanner.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NBitcoin.RPC;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
	{
        string dataDir = Configuration["datadir"] ?? EnvironmentHelpers.GetDataDir(Path.Combine("LookUp", "Backend"));
        string configFilePath = Path.Combine(dataDir, "Config.json");
        Config config = Config.LoadFile(configFilePath);

        Logger.Initialize(dataDir);

        services.AddSingleton(serviceProvider => config);

        string host = config.GetBitcoinRpcUri();
        RPCClient rpcClient = new(
            authenticationString: config.BitcoinRpcConnectionString,
            hostOrUri: host,
            network: config.Network);

        MinimalRPCClient myRPCClient = new MinimalRPCClient(rpcClient);
        services.AddSingleton<IRPCClient>(serviceProvider => myRPCClient);

        services.AddDbContextPool<MessageDatabaseContext>(options =>
            options.UseNpgsql(config.SQLConnectionString));

        services.AddScoped<MessageRepository>();

        services.AddSingleton<ScanStatus>();
        services.AddHostedService<ScannerService>();

        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "DataProtection")));

        // With a SizeLimit, every entry must set Size; MessageModelIdempotencyCache is the only user and counts each search as 1.
        services.AddMemoryCache(options => options.SizeLimit = MessageModelIdempotencyCache.MaxEntries);

        services.AddSingleton<MessageModelIdempotencyCache>();

        services.AddMvc();
        services.AddControllers();

        services.AddStartupTask<StartupTask>();

        services.AddEndpointsApiExplorer();

        services.AddAuthorization();

    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        app.UseCors("ScannerPolicy");
    }
}
