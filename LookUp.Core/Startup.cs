using LookUp.Core;
using LookUp.Core.Config;
using LookUp.Core.DataBase;
using LookUp.Core.Helpers;
using LookUp.Core.Models;
using LookUp.Core.Rpc;
using LookUp.Core.Services;
using Microsoft.EntityFrameworkCore;
using NBitcoin.RPC;
using System;

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
        var lastScannedBlockHeight = LastScannedBlockHeight.LoadFromFile(lastScannedBlockHeightFilePath);

        var scanChannel = new ScanChannel();

        var scannerService = new ScannerService(myRPCClient, lastScannedBlockHeight, scanChannel);
        services.AddHostedService<ScannerService>(services => scannerService);

        var dataBaseWiter = new DataBaseWriterService(scanChannel);
        services.AddHostedService<DataBaseWriterService>(services => dataBaseWiter);

        services.AddDbContext<MessageDatabaseContext>(options => options.UseNpgsql(config.SQLConnectionString));


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
