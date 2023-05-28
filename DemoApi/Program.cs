using Context;
using DemoApi.Utils;
using RabbitMq.Utils;
using RabbitMQ.Client;

namespace DemoApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSingleton<SettingsProvider>();
            builder.Services.AddDbContext<HashesContext>(ServiceLifetime.Transient);
            builder.Services.AddTransient<IHashesRepository, SqlHashesRepository>();
            builder.Services.AddTransient<IRabbitMqHelper, RabbitMqHelper>();
            builder.Services.AddSingleton<IConnection>((p) =>
            {
                var settingsProvider = p.GetService<SettingsProvider>() ?? throw new InvalidOperationException($"{nameof(SettingsProvider)} must be configured beforehand."); ;
                var mqHelper = p.GetService<IRabbitMqHelper>() ?? throw new InvalidOperationException($"{nameof(IRabbitMqHelper)} must be configured beforehand.");
                return mqHelper.CreateConnection(settingsProvider);
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}