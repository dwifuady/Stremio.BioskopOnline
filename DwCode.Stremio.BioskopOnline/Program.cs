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

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

const string bioskopOnlineUrl = "https://bioskoponline.com/";
const string addonsName = "Bioskop Online";

app.MapGet("/manifest.json", () => 
{
    return new Manifest
        (
            "com.stremio.bioskoponline.addon", 
            "1.1.0", 
            addonsName, 
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
            var meta = new Meta(data.Hashed_id
                , "movie"
                , data.Name
                , data.Images?.Portrait ?? ""
                , ""
                , data.Images?.Spotlight ?? ""
                , ""
                , ""
                , Array.Empty<string>()
                , Array.Empty<string>()
                , Array.Empty<string>()
                , Array.Empty<string>()
                );
            metas.Add(meta);
        }
    }

    return new SearchResult(metas);
});

app.MapGet("stream/movie/{id}", async (string id, HttpClient http) => 
{
    if (id.StartsWith("tt"))
    {
        return null;
    };

    var response = await http.GetFromJsonAsync<RootDetail>($"video/title?hashed_id={id.Replace(".json", "")}");
    string title = "";
    if (response?.Code == 200 && response?.Data is not null)
    {
        title = response.Data.Name;
        var price = response?.Data?.Price?.Normal;
        title += price > 0 ? $", {price} IDR" : "";
    }

    var streams = new List<Stream>
    {
        new Stream(title , $"{bioskopOnlineUrl}film/{id.Replace(".json", "")}", addonsName)
    };
    return new { streams };
});

app.MapGet("meta/movie/{id}", async (string id, HttpClient http) =>
{
    if (id.StartsWith("tt"))
    {
        return null;
    }

    var response = await http.GetFromJsonAsync<RootDetail>($"video/title?hashed_id={id.Replace(".json", "")}");
    if (response?.Code == 200 && response?.Data is not null)
    {
        var data = response.Data;
        var persons = data.Persons;
        var genres = data.Genres;

        List<string> cast = new();
        List<string> writer = new();
        List<string> director = new();
        List<string> genre = new();

        if (persons is not null)
        {
            cast.AddRange(persons.Where(p => p.Type == "Cast").Select(p => p.Name));
            writer.AddRange(persons.Where(p => p.Type == "Writer").Select(p => p.Name));
            director.AddRange(persons.Where(p => p.Type == "Director").Select(p => p.Name));
        }

        if (genres is not null)
        {
            genre.AddRange(genres.Select(x => x.Name));
        }

        var meta = new Meta(data.Hashed_id
            , "movie"
            , data.Name
            , data.Images.Portrait
            , data.Description
            , data.Images.Spotlight
            , $"{data.Movies.Duration} min"
            , $"{data.Movies.Release.Year}"
            , cast.ToArray()
            , writer.ToArray()
            , director.ToArray()
            , genre.ToArray()
            );

        return new { meta };
    }
    return new {
        meta = new Meta(id.Replace(".json","")
        , "movie"
        , "", "", "", "", "", ""
        , Array.Empty<string>()
        , Array.Empty<string>()
        , Array.Empty<string>()
        , Array.Empty<string>()
        )
    };
});

app.Run();

#region Stremio
record Manifest(string Id, string Version, string Name, string Description, Catalog[] Catalogs, Object[] Resources, string[] Types);
record Catalog(string Type, string Id, string Name, Extra[] Extra, string[] ExtraSupported);
record Extra(string Name, bool IsRequired);
record Stream(string Title, string ExternalUrl, string Name);
record SearchResult(IEnumerable<Meta> Metas);
record Meta(string Id, string Type, string Name, string Poster, string Description, string Background, string Runtime, string Year, string[] Cast, string[] Writer, string[] Director, string[] Genres);
#endregion

#region BioskopOnline
record Root(int Code, string Message, Data[] Data);
record RootDetail(int Code, string Message, Data Data);
record Data(string Hashed_id, string Name, Images Images, string Description, Movies Movies, Price Price, List<Person> Persons, List<Genre> Genres);
record Images(string Thumbnail, string Portrait, string Thumbnail_Portrait, string Spotlight);
record Movies(int Duration, DateTime Release);
record Price(decimal Normal, decimal? Promo);
record Person(string Hashed_id, string Name, string Type);
record Genre(string Hashed_id, string Name);
#endregion