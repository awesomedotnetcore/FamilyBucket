﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System;
using System.IO;

using Bucket.AspNetCore.Extensions;
using Bucket.Logging.EventSubscribe;
using Bucket.Logging.Events;
using Bucket.Tracing.EventSubscribe.Elasticsearch;
using Bucket.Tracing.Events;
using Bucket.Tracing.EventSubscribe;
using Bucket.EventBus.Extensions;
using Bucket.EventBus.Abstractions;
using Bucket.EventBus.RabbitMQ;
namespace Bucket.ConsoleApp
{
    class Program
    {
        private static IServiceProvider serviceProvider;
        static void Main(string[] args)
        {
            Console.WriteLine("品值基础事件订阅消费启动!");
            // 初始化
            Initialize();
            // 业务
            var eventBus = serviceProvider.GetRequiredService<IEventBus>();
            // 事件订阅
            eventBus.Subscribe<LogEvent, DbLogEventHandler>();
            eventBus.Subscribe<TracingEvent, TracingEventHandler>();
            eventBus.StartSubscribe();
            for (var i = 0; i < 200; i++)
            {
                eventBus.Publish(new TracingEvent(new Tracing.DataContract.Span { }));
            }
        }
        private static void Initialize()
        {
            // 添加配置文件
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory());
            builder.AddJsonFile("appsettings.json", true, true);
            var configuration = builder.Build();
            // 添加DI容器
            var services = new ServiceCollection().AddOptions().AddLogging();
            // 添加基础设施服务
            services.AddBucket();
            services.AddMemoryCache();
            // 添加事件驱动
            var eventConfig = configuration.GetSection("EventBus").GetSection("RabbitMQ");
            services.AddEventBus(option =>
            {
                option.UseRabbitMQ(opt =>
                {
                    opt.UserName = eventConfig["UserName"];
                    opt.Password = eventConfig["Password"];
                    opt.HostName = eventConfig["HostName"];
                    opt.Port = Convert.ToInt32(eventConfig["Port"]);
                    opt.QueueName = eventConfig["QueueName"];
                });
            });
            // 添加日志消费数据库配置
            services.AddSingleton(p => new DbLogOptions
            {
                ConnectionString = configuration.GetSection("SqlSugarClient")["ConnectionString"],
                DbShardingRule = 0,
                DbType = "MySql",
                IsDbSharding = false,
                IsWriteConsole = false
            });
            // 添加链路追踪ES消费配置
            services.Configure<ElasticsearchOptions>(configuration.GetSection("Elasticsearch"));
            services.AddSingleton<IIndexManager, IndexManager>();
            services.AddSingleton<IElasticClientFactory, ElasticClientFactory>();
            services.AddScoped<ISpanStorage, ElasticsearchSpanStorage>();
            // 
            services.AddTransient<DbLogEventHandler>();
            services.AddTransient<TracingEventHandler>();
            // 容器
            serviceProvider = services.BuildServiceProvider();
        }
    }
}
