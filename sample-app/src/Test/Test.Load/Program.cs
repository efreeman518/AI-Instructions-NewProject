using Microsoft.Extensions.Configuration;
using Test.Load;

Console.WriteLine("TaskFlow Load Tester");

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

string baseUrl = config.GetValue<string>("TaskFlowApi:BaseUrl")!;
TodoItemLoadTest.Run(baseUrl);
