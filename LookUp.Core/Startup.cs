using LookUp.Core.Config;
using LookUp.Core.Helpers;
using LookUp.Core.Rpc;
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
        services.AddSingleton<IRPCClient>(serviceProvider =>
        {
            string host = config.GetBitcoinRpcUri();
            RPCClient rpcClient = new(
                authenticationString: config.BitcoinRpcConnectionString,
                hostOrUri: host,
                network: config.Network);

            MyRPCClient myRPCClient = new MyRPCClient(rpcClient);
            return myRPCClient;
        });

        services.AddMemoryCache();
        services.AddMvc();
        services.AddControllers();

        services.AddEndpointsApiExplorer();

        services.AddAuthorization();

    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
