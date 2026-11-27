using Breeze.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;

namespace Breeze.AspNetCore.Tests {

  public class Person {
    public int PersonId { get; set; }

    // Not null, but '' is a value: the client's required validator must accept it.
    [Required(AllowEmptyStrings = true)]
    public string Nickname { get; set; } = "";

    [Required]
    public string Name { get; set; } = "";

    [Required(AllowEmptyStrings = true), MaxLength(10)]
    public string Code { get; set; } = "";

    public string? Notes { get; set; }
  }

  public class PeopleContext : DbContext {
    public PeopleContext(DbContextOptions<PeopleContext> options) : base(options) { }
    public DbSet<Person> People => Set<Person>();
  }

  /// <summary>
  /// The validators the EF Core metadata builder publishes for a data property. Built with
  /// <see cref="MetadataBuilder"/> and serialized as <c>EFPersistenceManager.BuildJsonMetadata</c>
  /// does, without creating a persistence manager (see <see cref="TestHost"/>).
  /// </summary>
  public class MetadataTests {

    private static JObject PersonType() {
      var options = new DbContextOptionsBuilder<PeopleContext>()
        .UseInMemoryDatabase(nameof(MetadataTests)).Options;
      using var context = new PeopleContext(options);
      var jss = new JsonSerializerSettings {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
      };
      jss.Converters.Add(new StringEnumConverter());
      var json = JObject.Parse(JsonConvert.SerializeObject(MetadataBuilder.BuildFrom(context), jss));
      return (JObject)json["structuralTypes"]!.Single(t => (string?)t["shortName"] == nameof(Person));
    }

    private static JArray Validators(string property) {
      var dp = PersonType()["dataProperties"]!.Single(p => (string?)p["nameOnServer"] == property);
      return (JArray)dp["validators"]!;
    }

    private static JObject? Required(string property) =>
      (JObject?)Validators(property).SingleOrDefault(v => (string?)v["name"] == "required");

    [Fact]
    public void Required_AllowEmptyStrings_is_published() {
      Assert.True(JToken.DeepEquals(
        JObject.Parse("""{ "name": "required", "allowEmptyStrings": true }"""), Required("Nickname")));
    }

    [Fact]
    public void Required_AllowEmptyStrings_keeps_maxLength() {
      Assert.True(JToken.DeepEquals(
        JObject.Parse("""{ "name": "required", "allowEmptyStrings": true }"""), Required("Code")));
      Assert.Contains(Validators("Code"), v => (string?)v["name"] == "maxLength" && (int?)v["maxLength"] == 10);
    }

    [Fact]
    public void Plain_Required_stays_plain() {
      Assert.True(JToken.DeepEquals(JObject.Parse("""{ "name": "required" }"""), Required("Name")));
    }

    [Fact]
    public void Non_nullable_without_the_attribute_stays_plain() {
      Assert.True(JToken.DeepEquals(JObject.Parse("""{ "name": "required" }"""), Required("PersonId")));
    }

    [Fact]
    public void Nullable_has_no_required() {
      Assert.Null(Required("Notes"));
    }
  }
}
