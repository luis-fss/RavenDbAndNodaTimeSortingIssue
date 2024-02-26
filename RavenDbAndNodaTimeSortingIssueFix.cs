using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Json.Serialization.NewtonsoftJson;
using Raven.TestDriver;
using Xunit;

namespace RavenDbAndNodaTimeSortingIssue;

public class RavenDbAndNodaTimeSortingIssueFix : RavenTestDriver
{
    [Fact]
    public async Task RavenDbAndNodaTimeSortingIssueFixTest()
    {
        var pattern = InstantPattern.ExtendedIso;

        // I think the issue is related to milliseconds; note that if the milliseconds have
        // exactly the same number of digits in all Instants, the sorting will be corrected.
        const string instantFormat1 = "2024-10-10T10:10:10.3370257Z";
        const string instantFormat2 = "2024-10-10T10:10:11.554013Z"; // this Instant has less digits than the others and will cause the error!!!
        //const string instantFormat2 = "2024-10-10T10:10:11.5540131Z"; // this Instant has the same quantity of digits than the others and will NOT cause any error!!!
        //const string instantFormat2 = "2024-10-10T10:10:11.5540130Z"; // this Instant has the same quantity of digits than the others, but with a zero at the end, this will cause a parse error NOT related with RavenBD!!!
        const string instantFormat3 = "2024-10-10T10:10:12.9545526Z";

        var person1 = new Person("1", pattern.Parse(instantFormat1).Value);
        var person2 = new Person("2", pattern.Parse(instantFormat2).Value);
        var person3 = new Person("3", pattern.Parse(instantFormat3).Value);

        // linq sorting
        var personExpectedOrderList = new List<Person> { person1, person2, person3 }.OrderBy(person => person.CreatedOn).ToList();

        // Asserting linq sorting just to prove it
        Assert.Equal(instantFormat1, pattern.Format(personExpectedOrderList[0].CreatedOn));
        Assert.Equal(instantFormat2, pattern.Format(personExpectedOrderList[1].CreatedOn));
        Assert.Equal(instantFormat3, pattern.Format(personExpectedOrderList[2].CreatedOn));

        var documentStore = GetDocumentStore();

        using (var storesSession = documentStore.OpenAsyncSession())
        {
            await storesSession.StoreAsync(person1);
            await storesSession.StoreAsync(person2);
            await storesSession.StoreAsync(person3);

            storesSession.Advanced.WaitForIndexesAfterSaveChanges();
            await storesSession.SaveChangesAsync();
        }

        List<Person> ravenOrderedList;

        using (var querySession = documentStore.OpenAsyncSession())
        {
            ravenOrderedList = await querySession.Query<Person>().OrderBy(person => person.CreatedOn).ToListAsync();
        }

        WaitForUserToContinueTheTest(documentStore);

        Assert.Equal(instantFormat1, pattern.Format(ravenOrderedList[0].CreatedOn)); // yeah, success!!!
        Assert.Equal(instantFormat2, pattern.Format(ravenOrderedList[1].CreatedOn));
        Assert.Equal(instantFormat3, pattern.Format(ravenOrderedList[2].CreatedOn));
    }

    protected override void PreInitialize(IDocumentStore documentStore)
    {
        ModifyDocumentStore(documentStore);
    }

    private static readonly InstantPattern Pattern = InstantPattern.CreateWithInvariantCulture("yyyy-MM-ddTHH:mm:ss.fffffff'Z'");

    private class InstantConverter : JsonConverter<Instant>
    {
        public override Instant ReadJson(JsonReader reader, Type type, Instant existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // DateTime value = serializer.Deserialize<DateTime>(reader);
            // return Instant.FromDateTimeUtc(value);
            var timestamp = serializer.Deserialize<string>(reader);
            return InstantPattern.ExtendedIso.Parse(timestamp).Value;
        }

        public override void WriteJson(JsonWriter writer, Instant instant, JsonSerializer serializer)
        {
            // DateTime value = instant.ToDateTimeUtc();
            // serializer.Serialize(writer, value);
            var timestamp = Pattern.Format(instant);
            serializer.Serialize(writer, timestamp);
        }
    }

    private static bool InstantQueryValueConverter(string name, Instant instant, bool range, out string outputValue)
    {
        var timestamp = Pattern.Format(instant);
        outputValue = timestamp;
        return true;
    }

    private static void ModifyDocumentStore(IDocumentStore documentStore)
    {
        var serializationConventions = (NewtonsoftJsonSerializationConventions)documentStore.Conventions.Serialization;
        var previousCustomSerializer = serializationConventions.CustomizeJsonSerializer;
        serializationConventions.CustomizeJsonSerializer = serializer =>
        {
            previousCustomSerializer?.Invoke(serializer);

            serializer.Converters.Add(new InstantConverter());
            serializer.DateParseHandling = DateParseHandling.None;
            serializer.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
        };

        documentStore.Conventions.RegisterQueryValueConverter<Instant>(InstantQueryValueConverter);
    }
}