using NodaTime;

namespace RavenDbAndNodaTimeSortingIssue;

public class Person
{
    public Person()
    {

    }

    public Person(string id, Instant createdOn)
    {
        Id = id;
        CreatedOn = createdOn;
    }

    public string Id { get; set; }
    public Instant CreatedOn { get; set; }
}