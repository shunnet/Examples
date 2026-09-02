using Microsoft.OpenApi;
using Snet.Modbus;
using Snet.Service;
using Snet.Sim;

namespace Snet.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 注册采集设备（模拟库，无需硬件即可演示）
            await builder.Services.AddDaqAsync(b =>
            {
                b.AddSim(new SimData.Basics { SN = "SIM-1" }).
                AddModbus(new ModbusData.Basics { SN = "MODBUS-1" });
            });

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(opt =>
            {
                opt.SwaggerDoc("v1", new OpenApiInfo { Title = "Snet", Version = "v1" });
                opt.IgnoreObsoleteActions();
                opt.IgnoreObsoleteProperties();

                foreach (var file in Directory.GetFiles(Path.GetDirectoryName(typeof(Program).Assembly.Location)))
                {
                    if (Path.GetExtension(file).Equals(".xml", StringComparison.CurrentCultureIgnoreCase))
                    {
                        opt.IncludeXmlComments(file, true);
                    }
                }
            });

            var app = builder.Build();

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
