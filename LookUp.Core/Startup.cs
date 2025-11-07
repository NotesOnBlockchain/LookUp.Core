using LookUp.Core.Rpc;
using LookUp.Core.Services;
using LookUp.Helpers;
using LookUp.Scanner;
using LookUp.Scanner.Config;
using LookUp.Scanner.DataBase;
using LookUp.Scanner.Helpers;
using LookUp.Scanner.LastScannedBlockHeight;
using LookUp.Scanner.Services;
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

        services.AddSingleton(serviceProvider => config);

        string host = config.GetBitcoinRpcUri();
        RPCClient rpcClient = new(
            authenticationString: config.BitcoinRpcConnectionString,
            hostOrUri: host,
            network: config.Network);

        MinimalRPCClient myRPCClient = new MinimalRPCClient(rpcClient);
        services.AddSingleton<IRPCClient>(serviceProvider => myRPCClient);

        string lastScannedBlockHeightFilePath = Path.Combine(dataDir, "LastScannedBlockHeight.txt");
        var lastScannedBlockHeight = LastScannedBlockHeightHolder.LoadFromFile(lastScannedBlockHeightFilePath);
        services.AddSingleton<LastScannedBlockHeightHolder>(services => lastScannedBlockHeight);

        var scanChannel = new ScanChannel();
        services.AddSingleton(scanChannel);

        services.AddDbContextPool<MessageDatabaseContext>(options =>
            options.UseNpgsql(config.SQLConnectionString));

        services.AddScoped<MessageRepository>();

        services.AddHostedService<ScannerService>();
        services.AddHostedService<DataBaseWriterService>();


        services.AddMemoryCache();
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
    }
}
