using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(UserAPI.Startup))]

namespace UserAPI
{
    class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {

            var configuration = new ConfigurationBuilder()
              .SetBasePath(Environment.CurrentDirectory)
              .AddJsonFile("local.settings.json", true, true)
              .AddEnvironmentVariables()
              .Build();
            var SqlConnection = configuration["Values:SqlConnectionString"];

            builder.Services.AddDbContext<UserDbContext>(
                options => options.UseSqlServer(SqlConnection));

            DocumentDBRepository<dynamic>.Initialize();
            builder.Services.AddSingleton<InsertActionFilterAttribute>();
            builder.Services.AddScoped<CosmosHelper<dynamic>>();
        }
    }
}
