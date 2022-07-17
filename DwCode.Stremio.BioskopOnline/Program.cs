var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped(hc => new HttpClient { BaseAddress = new Uri("https://v1-api.bioskoponline.com/") });
builder.Services.AddCors();
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(Convert.ToInt32(Environment.GetEnvironmentVariable("PORT")));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(corsPolicyBuilder => 
    corsPolicyBuilder
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()
);

app.UseDefaultFiles();
app.UseStaticFiles();

const string bioskopOnlineUrl = "https://bioskoponline.com/";
const string addonsName = "Bioskop Online";
const string metaTypeMovie = "movie";

app.MapGet("/manifest.json", () => new Manifest
(
    "com.stremio.bioskoponline.addon",
    "1.1.2",
    addonsName,
    "Search Indonesian movies that available on BioskopOnline",
    new List<Catalog>
    {
        new Catalog("movie", "bioskopOnlineMovies", "Bioskop Online Movies", new List<Extra> { new Extra("search", true) }, new List<string> {"search"})
    },
    new List<string>
    {
        "catalog", "meta", "stream"
    },
    new List<string> { metaTypeMovie }
));

app.MapGet("/catalog/movie/bioskopOnlineMovies/{search}", async (string search, HttpClient http) =>
{
    var keyword = search.Split("=")[1];
    var response = await http.GetFromJsonAsync<Root>($"video/searchAll?keyword={keyword.Split('.')[0]}");

    var metas = new List<Meta>();
    if (response?.Code != 200) return new SearchResult(metas);
    foreach (var data in response.Data)
    {
        var meta = new Meta(data.Hashed_id
            , metaTypeMovie
            , data.Name
            , data.Images?.Portrait ?? ""
            , ""
            , data.Images?.Spotlight ?? ""
        );
        metas.Add(meta);
    }

    return new SearchResult(metas);
});

app.MapGet("stream/movie/{id}", async (string id, HttpClient http) =>
{
    if (id.StartsWith("tt"))
    {
        return null;
    };
    
    var response = await http.GetFromJsonAsync<RootDetail>($"video/title?hashed_id={id.Split('.')[0]}");
    var title = "";
    if (response?.Code == 200 && response?.Data is not null)
    {
        title = response.Data.Name;
        var price = response?.Data?.Price?.Normal;
        title += price > 0 ? $", {price.Value.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("ID-id"))}" : "";
    }

    var streams = new List<Stream>
    {
        new(title , $"{bioskopOnlineUrl}film/{id.Split('.')[0]}", addonsName)
    };
    return new { streams };
});

app.MapGet("meta/movie/{id}", async (string id, HttpClient http) =>
{
    if (id.StartsWith("tt"))
    {
        return null;
    }

    var response = await http.GetFromJsonAsync<RootDetail>($"video/title?hashed_id={id.Split('.')[0]}");
    if (response?.Code != 200 || response?.Data is null)
        return new
        {
            meta = new Meta(id.Split('.')[0]
                , metaTypeMovie
                , ""
            )
        };
    var data = response.Data;
    var persons = data.Persons;
    var genres = data.Genres;

    List<string> cast = new();
    List<string> writer = new();
    List<string> director = new();

    cast.AddRange(persons.Where(p => p.Type == PersonType.Cast.ToString()).Select(p => p.Name));
    writer.AddRange(persons.Where(p => p.Type == PersonType.Writer.ToString()).Select(p => p.Name));
    director.AddRange(persons.Where(p => p.Type == PersonType.Director.ToString()).Select(p => p.Name));

    List<string> genre = new();
    genre.AddRange(genres.Select(x => x.Name));

    var meta = new Meta(data.Hashed_id
        , metaTypeMovie
        , data.Name
        , data.Images.Portrait
        , data.Description
        , data.Images.Spotlight
        , $"{data.Movies.Duration} min"
        , $"{data.Movies.Release.Year}"
        , cast
        , writer
        , director
        , genre
    );

    return new { meta };
});

app.Run();

#region Stremio
record Manifest(string Id, string Version, string Name, string Description, IEnumerable<Catalog> Catalogs, IEnumerable<string> Resources, IEnumerable<string> Types);
record Catalog(string Type, string Id, string Name, IEnumerable<Extra> Extra, IEnumerable<string> ExtraSupported);
record Extra(string Name, bool IsRequired);
record Stream(string Title, string ExternalUrl, string Name);
record SearchResult(IEnumerable<Meta> Metas);
record Meta(string Id, string Type, string? Name, string? Poster = "", string? Description = "", string? Background = "", string? Runtime = "", string? Year = "", IEnumerable<string>? Cast = null, IEnumerable<string>? Writer = null, IEnumerable<string>? Director = null, IEnumerable<string>? Genres = null);
#endregion

#region BioskopOnline
record Root(int Code, string Message, IEnumerable<Data> Data);
record RootDetail(int Code, string Message, Data Data);
record Data(string Hashed_id, string Name, Images Images, string Description, Movies Movies, Price Price, IEnumerable<Person> Persons, IEnumerable<Genre> Genres);
record Images(string Thumbnail, string Portrait, string Thumbnail_Portrait, string Spotlight);
record Movies(int Duration, DateTime Release);
record Price(double Normal, double? Promo);
record Person(string Hashed_id, string Name, string Type);
record Genre(string Hashed_id, string Name);
#endregion

public enum PersonType
{
    Cast,
    Writer,
    Director
}