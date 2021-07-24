using System.Linq;
using System;
using System.Collections.Generic;
using Nest;
namespace Nca.ElaticClient.App
{
    class Program
    {
        static void Main(string[] args)
        {
            var entityIndex = "entities";
            var settings = new ConnectionSettings(new Uri("http://localhost:9200"))
            .DefaultIndex(entityIndex);
            settings.DefaultMappingFor<Entity>(m => m
                    .IdProperty(p => p.Id)
                );

            var client = new ElasticClient(settings);
            // mapping

            var createIndexResponse = client.Indices.Create(entityIndex, c => c
                                        .Map<Entity>(m => m
                                            .AutoMap()
                                            .Properties(ps => ps
                                                .Nested<TagLog>(n => n
                                                  .AutoMap()
                                                  .Name(nn => nn.Tags))

                                            )
                                        )
                                    );

            Console.WriteLine("Index :", createIndexResponse.IsValid);
            var maxNumber = 1000 * 10;
            var entityNames = new List<string>(){
                "Student",
                "University",
                "Job",
                "Profressor"
            };
            var importData = false;
            if (importData)
            {
                // insert or update
                for (var i = 0; i < maxNumber; i++)
                {
                    var index = i % entityNames.Count;

                    var entity = new Entity()
                    {
                        Id = i,
                        Name = entityNames[index],
                        Tags = new List<TagLog>()
                    {
                        // valid
                        new TagLog(){ Id = 1, Name = $"log {i}", Tags = new List<string>() {$"tag{i}", "tagtest2"}, CreationDate = DateTime.Now.AddDays(1) },

                        new TagLog(){ Id = 1, Name = $"log {i}", Tags = new List<string>() {$"tag{i-1}", $"tag{i-1}"}, CreationDate = DateTime.Now.AddDays(-1) },

                        new TagLog(){ Id = 1, Name = $"log {i}", Tags = new List<string>() {$"tag{i-3}", $"tag{i-3}"}, CreationDate = DateTime.Now.AddDays(-2) }
                    }
                    };

                    string v = $"{i}";
                    var indexResponse = client.IndexDocument(entity);

                    Console.WriteLine($"Index document {i} :{indexResponse}");
                }
            }

            // get items

            var date = DateTime.Now;
            var dateNow = DateMath.Anchored(date);
            var searchResponse = client.Search<Entity>(s => s
                                        .Index(entityIndex)
                                        .From(0)
                                        .Size(maxNumber)
                                        .Query(q => q
                                            .Nested(c => c
                                                    .Boost(1.1)
                                                    .InnerHits(i => i.Explain())
                                                    .Path(p => p.Tags)
                                                    .Query(tags => +tags
                                                        .DateRange(d => d
                                                            .Field(r => r.Tags.First().CreationDate)
                                                            .LessThanOrEquals(dateNow)
                                                        ) && +tags
                                                        // .MatchPhrase(t => t
                                                        //     .Field(f => f.Tags.First().Name)
                                                        //     .Query("log 99")
                                                        // ) && +tags
                                                        .Terms(t => t
                                                            .Field(f => f.Tags.First().Tags)
                                                            .Terms("tag95", "tag96")
                                                        )
                                                    )
                                                    .IgnoreUnmapped()
                                                )
                                    ));

            //var searchResponse = client.Search<Entity>();
           
            var items = searchResponse.Documents;
            Console.WriteLine($"total: {searchResponse.Total}. Time ms: {searchResponse.Took}");
            foreach (var document in items)
            {
                Console.WriteLine($"Id: {document.Id} - Name:{document.Name} - {document.Tags.First().CreationDate}");
            }
        }
    }
}
