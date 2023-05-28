using BackgroundWorker.HostedServices;
using BackgroundWorker.Utils;
using Context;
using RabbitMq.Utils;

namespace BackgroundWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddHostedService<RabbitMqHostService>();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSingleton<SettingsProvider>();
            builder.Services.AddDbContext<HashesContext>(ServiceLifetime.Transient);
            builder.Services.AddTransient<IHashesRepository, SqlHashesRepository>();
            builder.Services.AddTransient<IRabbitMqHelper, RabbitMqHelper>();

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