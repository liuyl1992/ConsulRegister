using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ConsulRegister
{
    public static class RegisterToConsulExtension
    {
         /// <summary>
        /// 宿主机ip
        /// 通过docker run -e NODE_IP=192.168.66.66命令获得
        /// </summary>
        private static string HostIpV4Address { get; set; }
        
        /// <summary>
        /// Add Consul
        /// 添加consul
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddConsul(this IServiceCollection services, IConfiguration configuration)
        {
            // configuration Consul register address
            //配置consul注册地址
            services.Configure<ServiceDiscoveryOptions>(configuration.GetSection("ServiceDiscovery"));
            
            //configuration Consul client
            //配置consul客户端
            services.AddSingleton<IConsulClient>(sp => new Consul.ConsulClient(config =>
            {
                var consulOptions = sp.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(consulOptions.Consul.HttpEndPoint))
                {
                    config.Address = new Uri(consulOptions.Consul.HttpEndPoint);
                }
            }));

            return services;
        }

        /// <summary>
        /// use Consul
        /// 使用consul
        /// The default health check interface format is http://host:port/HealthCheck
        /// 默认的健康检查接口格式是 http://host:port/HealthCheck
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseConsul(this IApplicationBuilder app)
        {
            IConsulClient consul = app.ApplicationServices.GetRequiredService<IConsulClient>();
            IApplicationLifetime appLife = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
            IOptions<ServiceDiscoveryOptions> serviceOptions = app.ApplicationServices.GetRequiredService<IOptions<ServiceDiscoveryOptions>>();
            var features = app.Properties["server.Features"] as FeatureCollection;
            //http://+:80
            if (!int.TryParse(features.Get<IServerAddressesFeature>().Addresses.FirstOrDefault()?.Split(':')[2], out int port))
                port = 80;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"command get host: {HostIpV4Address}");
            Console.WriteLine($"real port: {port}");
            
            if (string.IsNullOrEmpty(HostIpV4Address))
            {
                if (!string.IsNullOrWhiteSpace(serviceOptions.Value.SelfAddress))
                {
                    var addressTemp = serviceOptions.Value.SelfAddress.Split(':');
                    HostIpV4Address = addressTemp[0];
                    Console.WriteLine($"config host: {addressTemp[0]}");
                    port = int.Parse(addressTemp[1]);
                    Console.WriteLine($"config ort: {addressTemp[1]}");
                }
                else
                {
                    HostIpV4Address = "localhost";
                    Console.WriteLine($"config host: {HostIpV4Address}");
                }
            }
            var serviceId = $"{serviceOptions.Value.ServiceName}_{HostIpV4Address}:{port}";

            var httpCheck = new AgentServiceCheck()
            {
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
                Interval = TimeSpan.FromSeconds(30),
                HTTP = $"{Uri.UriSchemeHttp}://{HostIpV4Address}:{port}/HealthCheck",
            };

            var registration = new AgentServiceRegistration()
            {
                Checks = new[] { httpCheck },
                Address = HostIpV4Address.ToString(),
                ID = serviceId,
                Name = serviceOptions.Value.ServiceName,
                Port = port
            };

            consul.Agent.ServiceRegister(registration).GetAwaiter().GetResult();

            // 服务应用停止后发注册RestApi服务,停止后不通知consul剔除
            //appLife.ApplicationStopping.Register(() =>
            //{
            //    consul.Agent.ServiceDeregister(serviceId).GetAwaiter().GetResult();
            //});

            Console.WriteLine($"健康检查服务:{httpCheck.HTTP}");


            app.Map("/HealthCheck", s =>
            {
                s.Run(async context =>
                {
                    await context.Response.WriteAsync("ok");
                });
            });
            return app;
        }
    }
}
