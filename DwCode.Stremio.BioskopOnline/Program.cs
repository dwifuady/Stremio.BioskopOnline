using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped(hc => new HttpClient { BaseAddress = new Uri("https://v1-api.bioskoponline.com/") });
builder.Services.AddCors();
builder.WebHost.ConfigureKestrel(serverOptions => {
    serverOptions.Listen(IPAddress.Any, Convert.ToInt32(Environment.GetEnvironmentVariable("PORT")));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors(builder => 
builder 
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()
);

// app.UseHttpsRedirection();

const string bioskopOnlineUrl = "https://bioskoponline.com/";

app.MapGet("/manifest.json", () => 
{
    return new Manifest
        (
            "com.stremio.bioskoponline.addon", 
            "0.0.1", 
            "Bioskop Online", 
            "Search Indonesian movies that available on BioskopOnline", 
            new Catalog[]
            {
                new Catalog("movie", "bioskopOnlineMovies", "Bioskop Online Movies", new Extra[] { new Extra("search", true) }, new[] {"search"})
            },
            new [] 
            {
                "catalog", "meta", "stream"
            },
            new string[] { "movie" }
        );
});

app.MapGet("/catalog/movie/bioskopOnlineMovies/{search}", async (string search, HttpClient http) => 
{
    var keyword = search.Split("=")[1];
    var response = await http.GetFromJsonAsync<Root>($"video/searchAll?keyword={keyword.Replace(".json","")}");
    
    var metas = new List<Meta>();
    if (response?.Code == 200)
    {
        foreach (var data in response.Data)
        {
            var meta = new Meta(data.Hashed_id, "movie", data.Name, data.Images?.Portrait ?? "", "");
            metas.Add(meta);
        }
    }

    return new SearchResult(metas);
});

app.MapGet("stream/movie/{id}", (string id) => 
{
    var streams = new List<Stream>
    {
        new Stream("Bioskop Online", $"{bioskopOnlineUrl}film/{id.Replace(".json", "")}")
    };
    return new { streams = streams };
});

app.MapGet("meta/movie/{id}", async (string id, HttpClient http) =>
{
    var response = await http.GetFromJsonAsync<RootDetail>($"video/title?hashed_id={id.Replace(".json", "")}");
    if (response?.Code == 200)
    {
        var data = response?.Data;
        var meta = new Meta(data.Hashed_id, "movie", data.Name, data.Images.Portrait, data.Description);

        return new { meta = meta };
    }
    return new { meta = new Meta(id.Replace(".json",""), "movie", "", "", "")};
});

app.Run();

#region Stremio
record Manifest(string Id, string Version, string Name, string Description, Catalog[] Catalogs, Object[] Resources, string[] Types);

record Catalog(string Type, string Id, string Name, Extra[] Extra, string[] ExtraSupported);

record Extra(string Name, bool IsRequired);

record Stream(string Title, string ExternalUrl);

record SearchResult(IEnumerable<Meta> metas);

record Meta(string Id, string Type, string Name, string Poster, string Description);
#endregion

#region BioskopOnline

record Root(int Code, string Message, Data[] Data);
record RootDetail(int Code, string Message, Data Data);

record Data(string Hashed_id, string Name, Images Images, string Description);

record Images(string Thumbnail, string Portrait, string Thumbnail_Portrait);

#endregion