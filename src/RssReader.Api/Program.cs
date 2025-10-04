using RssReader.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRssReaderData("mongodb");

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
