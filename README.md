# Project's Installation

docker pull docker.elastic.co/elasticsearch/elasticsearch:7.13.4

docker run -p 9200:9200 -p 9300:9300 -e "discovery.type=single-node" docker.elastic.co/elasticsearch/elasticsearch:7.13.4

cd Presentation/Nca.ElasticClient.Console
dotnet run

# Solution for Matching System : Entity 2 Entites

## 
This project use Elastic Search engine to solve matching entity to entities problem.
Each entity having there own tags (keys) and be updated by times
Matching Algorithm should returen score between 2 entities.

## Idea
Build search query having scores to measurment best matched for entity to entites.
Search query must be fast


# Data design structure json
```json
{
  "_index": "entities",
  "_type": "_doc",
  "_id": "98",
  "_version": 41,
  "_seq_no": 52824,
  "_primary_term": 5,
  "found": true,
  "_source": {
    "id": 98,
    "name": "Job",
    "tags": [
      {
        "id": 1,
        "name": "log 98",
        "tags": [
          "tag_id98",
          "tag98",
          "tagtest2",
          "ggg"
        ],
        "creationDate": "2021-08-01T09:08:39.1018370+07:00",
        "deletionDate": "9999-12-31T23:59:59.9999999"
      },
      {
        "id": 1,
        "name": "log 98",
        "tags": [
          "tag_id98",
          "tag97",
          "tag97",
          "ggg"
        ],
        "creationDate": "2021-07-30T09:08:39.1233520+07:00",
        "deletionDate": "2021-08-01T09:08:39.1018370+07:00"
      },
      {
        "id": 1,
        "name": "log 98",
        "tags": [
          "tag_id98",
          "tag95",
          "tag95",
          "ggg"
        ],
        "creationDate": "2021-07-29T09:08:39.1233540+07:00",
        "deletionDate": "2021-07-30T09:08:39.1233520+07:00"
      }
    ]
  }
}
```
# Build query in C#

## Insert data 
10.000 records

## Build query
``` c#
private static QueryContainer BuildSearchTerms(QueryContainerDescriptor<Entity> tags)
        {
            var container = tags.Terms(t => t
                            .Boost(0.1)
                            .Field(f => f.Tags.First().Tags)
                            .Terms(searchTerms)
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
                    )
                ) | q
                .MatchAll()
            );
        }
```
## Result
```
Query 7/31/2021 10:09:42 AM.total: 10000. Time ms: 43
Id: 95- Score: 2.05 - Name:Profressor - 8/1/2021 9:08:39 AM
Id: 96- Score: 1.8399999 - Name:Student - 8/1/2021 9:08:39 AM
Id: 0- Score: 1.63 - Name:Student - 8/1/2021 9:08:39 AM
Id: 1- Score: 1.63 - Name:University - 8/1/2021 9:08:39 AM
Id: 2- Score: 1.63 - Name:Job - 8/1/2021 9:08:39 AM
Id: 3- Score: 1.63 - Name:Profressor - 8/1/2021 9:08:39 AM
Id: 4- Score: 1.63 - Name:Student - 8/1/2021 9:08:39 AM
Id: 5- Score: 1.63 - Name:University - 8/1/2021 9:08:39 AM
Id: 6- Score: 1.63 - Name:Job - 8/1/2021 9:08:39 AM
Id: 7- Score: 1.63 - Name:Profressor - 8/1/2021 9:08:39 AM
```