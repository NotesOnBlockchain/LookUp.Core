using System;

public class Startup
{
	public Startup()
	{
	}

    public void ConfigureServices(IServiceCollection services)
	{
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
        app.UseSwagger();
    }
}
