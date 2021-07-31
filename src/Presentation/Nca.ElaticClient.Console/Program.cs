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
                var maxDate = DateTime.MaxValue;
                var lastCreatedDate = DateTime.Now.AddDays(1);
                var secondCreatedDate = DateTime.Now.AddDays(-1);
                var thirdCreatedDate = DateTime.Now.AddDays(-2);
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
                        new TagLog(){ Id = 1, Name = $"log {i}", Tags = new List<string>() {$"tag_id{i}", $"tag{i}", "tagtest2" , "ggg"}, CreationDate = lastCreatedDate , DeletionDate = maxDate},

                        new TagLog(){ Id = 1, Name = $"log {i}", Tags = new List<string>() {$"tag_id{i}",$"tag{i-1}", $"tag{i-1}", "ggg"}, CreationDate = secondCreatedDate, DeletionDate = lastCreatedDate },

                        new TagLog(){ Id = 1, Name = $"log {i}", Tags = new List<string>() {$"tag_id{i}",$"tag{i-3}", $"tag{i-3}", "ggg"}, CreationDate = thirdCreatedDate, DeletionDate = secondCreatedDate }
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
            var query = GetTrackScores();

            var searchResponse = client.Search<Entity>(query);

            var items = searchResponse.Documents;
            Console.WriteLine($"Query {date}.total: {searchResponse.Total}. Time ms: {searchResponse.Took}");
            foreach (var document in items)
            {
                double score = 0;
                if (searchResponse.HitsMetadata != null && searchResponse.HitsMetadata.Hits != null)
                {
                    var hit = searchResponse.HitsMetadata?.Hits.FirstOrDefault(h => h.Id.Equals(document.Id.ToString()));
                    if (hit != null && hit.Score.HasValue)
                    {
                        score = hit.Score.Value;
                        if (hit.Fields != null)
                        {
                            foreach (var item in hit.Fields)
                            {
                                Console.WriteLine($"Key-{item.Key}: {item.Value}");
                            }
                        }
                    }
                }
                Console.WriteLine($"Id: {document.Id}- Score: {score} - Name:{document.Name} - {document.Tags.First().CreationDate}");
            }
        }

        private static List<string> searchTerms = new List<string>() {"tag_id95" ,"tag94", "tag95", "ggg" };
        private static QueryContainer BuildSearchTerms(QueryContainerDescriptor<Entity> tags)
        {
            var container = tags.Terms(t => t
                            .Boost(0.1)
                            .Field(f => f.Tags.First().Tags)
                            .Terms("tag_id95", "tag95", "tag94", "tag99")
                        );
            foreach(var tag in searchTerms)
            {
                Console.WriteLine($"tag - {tag}");
                container = container | tags.Terms(t => t
                            .Boost(0.1)
                            .Field(f => f.Tags.First().Tags)
                            .Terms(tag)
                        );
            }
            return container;
        }
        private static SearchDescriptor<Entity> GetTrackScores()
        {
            var s = new SearchDescriptor<Entity>();
            var entityIndex = "entities";
            var date = DateTime.Now;
            var dateNow = DateMath.Anchored(date);


            return s
            .Index(entityIndex)
            .From(0)
            .Size(10)
            .TrackScores(true)
            .Explain()
            .Sort(sort => sort.Descending(SortSpecialField.Score))
            .Query(q => q
                // .Match(mm => mm
                //             .Boost(1.1)
                //             .Field(fff => fff.Name)
                //                 .Query("profressor")
                // ) | q
                .Nested(c => c
                    .ScoreMode(NestedScoreMode.Sum)
                    .Boost(2.1)
                    .InnerHits(i => i.Explain())
                    .IgnoreUnmapped()
                    .Path(p => p.Tags)
                    .Query(tags => tags
                        .DateRange(d => d
                            .Boost(0.1)
                            .Field(r => r.Tags.First().CreationDate)
                            .LessThan(dateNow)
                        ) && +tags
                        .DateRange(d => d
                            .Boost(0.1)
                            .Field(r => r.Tags.First().DeletionDate)
                            .GreaterThan(dateNow)
                        ) && BuildSearchTerms(tags) 
                        // .MultiMatch(mm => mm
                        //     .Query("tag")
                        //     .Fields(f => f.Field(ff => ff.Tags.First()))
                        // )
                        // .Terms(t => t
                        //     .Boost(0.5)
                        //     .Field(f => f.Tags.First().Tags)
                        //     .Terms("tag_id95", "tag95", "tag94", "tag99")
                        // )
                        // .MatchPhrase(t => t
                        //     .Field(f => f.Tags.First().Name)
                        //     .Query("log 99")
                        // ) && +tag
                    )
                ) | q
                .MatchAll()
            );
        }
        private static SearchDescriptor<Entity> GetQuery()
        {
            var s = new SearchDescriptor<Entity>();
            var entityIndex = "entities";
            var date = DateTime.Now;
            var dateNow = DateMath.Anchored(date);
            var searchTerms = new List<string>() { "tag94", "tag95", "ggg" };
            Func<QueryContainerDescriptor<Entity>, QueryContainer> getSelector = tags =>
            {
                var container = +tags
                 .DateRange(d => d
                      .Field(r => r.Tags.First().CreationDate)
                      .LessThan(dateNow)
                  ) && +tags
                  .DateRange(d => d
                      .Field(r => r.Tags.First().DeletionDate)
                      .GreaterThan(dateNow)
                  );
                for (var i = 0; i < searchTerms.Count - 1; i++)
                {
                    var list = new List<string>();
                    for (var j = 0; j < i; j++)
                    {
                        list.Add(searchTerms[j]);
                    }
                    if (list.Any())
                    {
                        Console.WriteLine($"terms : {string.Join(",", list)}");

                        tags
                        .Terms(t => t
                        .Field(f => f.Tags.First().Tags)
                        .Terms(list));
                    }
                }

                return container;
            };
            return s
            .Index(entityIndex)
            .From(0)
            .Size(100)
            .TrackScores(true)
            .Sort(sort => sort.Descending(SortSpecialField.Score))
            .Query(q => q
                .Bool(b => b
                    .Should(m => m
                        .Nested(c => c
                            .Path(p => p.Tags)
                            .Boost(1.3)
                            .Query(getSelector)
                        )
                    )
                    .Must(f => f.MatchAll())
                    //.MinimumShouldMatch(1)
                    .Boost(1))

            );
        }

    }
}
