using Microsoft.SemanticKernel;

namespace Codeblaze.SemanticKernel.Connectors.Memory.Neo4j;

public interface INeo4jQueryFactory
{
    public string TextProperty { get; }
    public string IndexProperty { get; }
    (string, object) ListIndexQuery();
    (string, object) CreateIndexQuery(string collectionName);
    (string, object) DropIndexQuery(string collectionName);
    (string, object) UpsertQuery(string collectionName, string key, ReadOnlyMemory<float> embedding);
    (string, object) UpsertBatchQuery(string collectionName, IEnumerable<string> keys,
        IEnumerable<ReadOnlyMemory<float>> embeddings);
    (string, object) GetQuery(string collectionName, string keys);
    (string, object) GetBatchQuery(string collectionName, IEnumerable<string> keys);
    (string, object) RemoveQuery(string collectionName, string keys);
    (string, object) RemoveBatchQuery(string collectionName, IEnumerable<string> keys);
    (string, object) GetNearestMatchQuery(string collectionName, ReadOnlyMemory<float> embedding,
        double minRelevanceScore, int limit = 1);
}

public abstract class Neo4jVectorQueries
{
    public const string ListIndexQuery = "SHOW VECTOR INDEXES YIELD name";

    public const string CreateIndexQuery = """
                                           CREATE VECTOR INDEX `$name`
                                           FOR (n:$node) ON (n.$indexProperty)
                                           OPTIONS {indexConfig: {
                                            `vector.dimensions`: $dimensions,
                                            `vector.similarity_function`: 'cosine'
                                           }
                                           """;

    public const string DropIndexQuery = "DROP INDEX $name";
    
    public const string GetNearestMatchQuery = """
                                               CALL db.index.vector.queryNodes($name, $limit, $embedding)
                                               YIELD node, score
                                               WHERE score >= $minRelevanceScore
                                               RETURN node.id AS id, score
                                               """;
    
    public const string UpsertQuery = """
                                      MATCH (n:$node {id: $id})
                                      CALL db.create.setNodeVectorProperty(n, $indexProperty, $embedding)
                                      RETURN node.id AS id
                                      """;
    
    public const string UpsertBatchQuery = """
                                           UNWIND $updates AS update, 
                                           MATCH (n:$node {id: update.id})
                                           CALL db.create.setNodeVectorProperty(n, $indexProperty, update.embedding)
                                           RETURN node.id AS id
                                           """;
    
    public const string GetQuery = """
                                   MATCH (n:$node {id: $id})
                                   RETURN n
                                   """;
    
    public const string GetBatchQuery = """
                                        UNWIND $ids AS id
                                        MATCH (n:$node {id: id})
                                        RETURN n
                                        """;
    
    public const string RemoveQuery = """
                                      MATCH (n:$node {id: $id})
                                      DELETE n
                                      """;
    
    public const string RemoveBatchQuery = """
                                           UNWIND $ids AS id
                                           MATCH (n:$node {id: id})
                                           DELETE n
                                           """;
}

/// <summary>
/// Neo4j 5.15 or above is supported by this query factory, vector functionality was introduced in an earlier version of neo4j
/// If you are using an old version you may need to implement some of the queries below using old cypher, refer neo4j docs
/// </summary>
/// <param name="name">Index name</param>
/// <param name="node">Node on which index is created</param>
/// <param name="indexProperty">Property of the node on which index is created</param>
/// <param name="dimensions">Vector index dimensions</param>
public class Neo4jVectorIndexQueryFactory(string name, string node, string indexProperty, string textProperty, int dimensions)
    : INeo4jQueryFactory
{
    /// <summary>
    /// This can be updated at runtime to pass different values to the query factory
    /// To use dynamic properties the default query methods need to be overriden
    /// and the dynamic property must be returned along with the new query
    /// </summary>
    public IDictionary<string, object> DynamicProperties { get; } = new Dictionary<string, object>();

    public string TextProperty => textProperty;
    public string IndexProperty => indexProperty;

    public virtual (string, object) ListIndexQuery()
    {
        return (Neo4jVectorQueries.ListIndexQuery, null);
    }

    public virtual (string, object) CreateIndexQuery(string collectionName)
    {
        if (name != collectionName)
            throw new KernelException(
                $"Kernel passed {collectionName} index but query factory is configured with {name} index");

        return (Neo4jVectorQueries.CreateIndexQuery, new
            {
                name, node, indexProperty, dimensions
            }
        );
    }

    public virtual (string, object) DropIndexQuery(string collectionName)
    {
        if (name != collectionName)
            throw new KernelException(
                $"Kernel passed {collectionName} index but query factory is configured with {name} index");

        return (Neo4jVectorQueries.DropIndexQuery, new { name });
    }

    public (string, object) UpsertQuery(string collectionName, string key, ReadOnlyMemory<float> embedding)
    {
        if (name != collectionName)
            throw new KernelException(
                $"Kernel passed {collectionName} index but query factory is configured with {name} index");

        return (Neo4jVectorQueries.UpsertQuery, new { id = key, node, embedding });
    }

    public (string, object) UpsertBatchQuery(string collectionName, IEnumerable<string> keys, IEnumerable<ReadOnlyMemory<float>> embeddings)
    {
        if (name != collectionName)
            throw new KernelException(
                $"Kernel passed {collectionName} index but query factory is configured with {name} index");

        var updates = keys.Zip(embeddings).Select((id, embedding) => new { id, embedding }).ToArray();

        return (Neo4jVectorQueries.UpsertBatchQuery, new { node, updates });
    }

    public virtual (string, object) GetNearestMatchQuery(string collectionName, ReadOnlyMemory<float> embedding,
        double minRelevanceScore, int limit = 1)
    {
        if (name != collectionName)
            throw new KernelException(
                $"Kernel passed {collectionName} index but query factory is configured with {name} index");

        return (Neo4jVectorQueries.GetNearestMatchQuery, new
        {
            name,
            limit,
            embedding = embedding.ToArray(),
            minRelevanceScore
        });
    }
    
    

    public virtual (string, object) GetQuery(string collectionName, string key)
    {
        if (name != collectionName)
            throw new KernelException(
                $"Kernel passed {collectionName} index but query factory is configured with {name} index");

        return (Neo4jVectorQueries.GetQuery, new { id = key, node });
    }

    public virtual (string, object) GetBatchQuery(string collectionName, IEnumerable<string> keys)
    {
        if (name != collectionName)
            throw new KernelException(
                $"Kernel passed {collectionName} index but query factory is configured with {name} index");

        return (Neo4jVectorQueries.GetBatchQuery, new { ids = keys.ToArray(), node });
    }

    public virtual (string, object) RemoveQuery(string collectionName, string key)
    {
        if (name != collectionName)
            throw new KernelException(
                $"Kernel passed {collectionName} index but query factory is configured with {name} index");

        return (Neo4jVectorQueries.RemoveQuery, new { id = key, node });
    }

    public virtual (string, object) RemoveBatchQuery(string collectionName, IEnumerable<string> keys)
    {
        if (name != collectionName)
            throw new KernelException(
                $"Kernel passed {collectionName} index but query factory is configured with {name} index");

        return (Neo4jVectorQueries.RemoveBatchQuery, new { ids = keys, node });
    }
}