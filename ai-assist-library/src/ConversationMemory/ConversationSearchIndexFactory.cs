using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace AiAssistLibrary.ConversationMemory;

internal static class ConversationSearchIndexFactory
{
 public static SearchIndex Create(string indexName, int dims)
 {
 var fields = new List<SearchField>
 {
 new SimpleField("id", SearchFieldDataType.String){ IsKey = true, IsFilterable = true },
 new SearchableField("sessionId"){ IsFilterable = true, IsFacetable = true },
 new SimpleField("t0", SearchFieldDataType.Double){ IsFilterable = true, IsSortable = true },
 new SimpleField("t1", SearchFieldDataType.Double){ IsFilterable = true, IsSortable = true },
 new SimpleField("speaker", SearchFieldDataType.String){ IsFilterable = true },
 new SimpleField("kind", SearchFieldDataType.String){ IsFilterable = true },
 new SimpleField("parentActId", SearchFieldDataType.String){ IsFilterable = true },
 new SearchableField("text"),
 new SearchField("textVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
 {
 IsFilterable = false,
 IsSearchable = false,
 VectorSearchDimensions = dims,
 VectorSearchProfileName = "vector-config"
 }
 };

 var index = new SearchIndex(indexName)
 {
 Fields = fields,
 VectorSearch = new VectorSearch
 {
 Algorithms = { new HnswAlgorithmConfiguration("hnsw-algo") },
 Profiles = { new VectorSearchProfile("vector-config", "hnsw-algo") }
 }
 };
 return index;
 }
}
