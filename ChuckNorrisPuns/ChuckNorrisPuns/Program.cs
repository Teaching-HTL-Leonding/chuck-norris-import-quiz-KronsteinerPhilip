using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

HttpClient client = new HttpClient();
var factory = new ChuckNorrisContextFactory();
using var dbContext = factory.CreateDbContext();

int numberOfJokes = 0;
if(args[0] == "clear")
{
    await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ChuckPuns");
    Console.ReadKey();
    Environment.Exit(1);
}
else if (Convert.ToInt32(args[0]) > 10)
{
    Console.WriteLine("Only Chuck Norris can take so much facts about him at a time!\nTry again with a smaller amount.");
    Console.ReadKey();
    Environment.Exit(1);
}

if (args.Length.Equals(0))
    numberOfJokes = 5;
else
    numberOfJokes = Convert.ToInt32(args[0]);

using var transaction = await dbContext.Database.BeginTransactionAsync();
try
{
    for (int i = 0; i < numberOfJokes; i++)
        await getPuns(client, factory);
    await transaction.CommitAsync();
}
catch (SqlException ex)
{
    Console.WriteLine(ex);
}

async Task getPuns(HttpClient client, ChuckNorrisContextFactory factory)
{
    string responseBody = "";
    try
    {
        HttpResponseMessage response = await client.GetAsync("https://api.chucknorris.io/jokes/random");
        response.EnsureSuccessStatusCode();
        responseBody = await response.Content.ReadAsStringAsync();
        
        Console.WriteLine(responseBody);
    }
    catch (HttpRequestException e)
    {
        Console.WriteLine("\nException Caught!");
        Console.WriteLine("Message :{0} ", e.Message);
    }
    await writeToDataBase(responseBody,factory);
}
async Task writeToDataBase(string serialized, ChuckNorrisContextFactory factory)
{
    var pun = new ChuckPun();
    int i = 0;
    do
    {
        using var dbContext = factory.CreateDbContext();
        var options = new JsonSerializerOptions()
        {
            IncludeFields = true,
        };
        var jsonContext = JsonSerializer.Deserialize<JsonContext>(serialized, options);

        pun = new ChuckPun { ChuckNorrisID = jsonContext.id, Url = jsonContext.url, Joke = jsonContext.value };
        i++;
    } 
    while (dbContext.ChuckPuns.Contains(pun));
    if(i > 10)
    {
        Console.WriteLine("Es sind bereits alle Puns vorhanden! Nur Chuck Norris könnte neue einfügen.");
        Environment.Exit(1);
    }
    dbContext.Add(pun);
    await dbContext.SaveChangesAsync();
}

public class JsonContext
{
    public string[] categories = {};
    public string ?created_at { get; set; }
    public string ?icon_url { get; set; }
    public string ?id { get; set; }
    public string ?updated_at { get; set; }
    public string ?url { get; set; }
    public string ?value { get; set; }
}
class ChuckPun
{
    public int Id { get; set; }

    [MaxLength(40)]
    public string ChuckNorrisID { get; set; } = "";

    [MaxLength(1024)]
    public string Url { get; set; } = "";

    public string Joke { get; set; } = "";
}
class ChuckNorrisContext : DbContext
{
    public DbSet<ChuckPun> ChuckPuns { get; set; }
#pragma warning disable CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
    public ChuckNorrisContext(DbContextOptions<ChuckNorrisContext> options) : base(options) { }
#pragma warning restore CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
}
class ChuckNorrisContextFactory : IDesignTimeDbContextFactory<ChuckNorrisContext>
{
    public ChuckNorrisContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<ChuckNorrisContext>();
        optionsBuilder
            // Uncomment the following line if you want to print generated
            // SQL statements on the console.
            .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new ChuckNorrisContext(optionsBuilder.Options);
    }
}