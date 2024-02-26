using NodaTime;

namespace RavenDbAndNodaTimeSortingIssue;

public class Person(string id, Instant createdOn)
{
    public string Id { get; set; } = id;
    public Instant CreatedOn { get; set; } = createdOn;
}