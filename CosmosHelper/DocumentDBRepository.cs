using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Linq.Expressions;
using Newtonsoft.Json;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.Documents;

namespace UserAPI
{
    public static class DocumentDBRepository<T> where T : class
    {

        private static readonly string Endpoint = "ADD_YOUR_COSMOS_ENDPOINT";
        private static readonly string Key = "ADD_KEY_FROM_YOUR_COSMOS";
        private static readonly string DatabaseId = "DATABASE_NAME_YOUR_DESIRE";
        private static DocumentClient client;
        public static string CollectionId = string.Empty;
        private static readonly FeedOptions DefaultOptions = new FeedOptions { EnableCrossPartitionQuery = true };

        public static async Task<T> GetItemAsync(string id)
        {
            try
            {
                Document document = await client.ReadDocumentAsync(UriFactory.CreateDocumentUri(DatabaseId, CollectionId, id));
                return (T)(dynamic)document;
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
        }

        public static async Task<IEnumerable<T>> GetItemsAsync(string collectionName, Expression<Func<T, bool>> predicate)
        {
            CollectionId = collectionName;

            IDocumentQuery<T> query = client.CreateDocumentQuery<T>(
                UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId),
                new FeedOptions { MaxItemCount = -1 })
                .Where(predicate)
                .AsDocumentQuery();

            List<T> results = new List<T>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<T>());
            }

            return results;
        }

        public static async Task<Document> CreateItemAsync(string jsonString, string collection)
        {
            try
            {
                CollectionId = collection;
                Initialize();
                CreateCollectionIfNotExistsAsync().Wait();
                CreateUDF();

                return await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId), JsonConvert.DeserializeObject(jsonString));
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        //public static async Task<Document> CreateItemAsync(T item, string collection)
        //{
        //    try
        //    {
        //        CollectionId = collection;
        //        Initialize();
        //        CreateCollectionIfNotExistsAsync().Wait();

        //        return await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId), item);
        //    }
        //    catch (Exception ex)
        //    {
        //        return null;
        //    }
        //}

        public static async Task<Document> UpdateItemAsync(string id, T item)
        {
            return await client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(DatabaseId, CollectionId, id), item);
        }

        public static async Task DeleteItemAsync(string id)
        {
            await client.DeleteDocumentAsync(UriFactory.CreateDocumentUri(DatabaseId, CollectionId, id));
        }

        public static void Initialize()
        {
            if (client == null)
            {
                client = new DocumentClient(new Uri(Endpoint), Key);
                CreateDatabaseIfNotExistsAsync().Wait();
            }
        }

        private static async Task CreateDatabaseIfNotExistsAsync()
        {
            try
            {
                await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(DatabaseId));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await client.CreateDatabaseAsync(new Microsoft.Azure.Documents.Database { Id = DatabaseId });
                }
                else
                {
                    throw;
                }
            }
        }

        public static async Task<IList<dynamic>> QueryDataByLastRefreshTime(string collectionName, string lastUpdatedTime)
        {
            try
            {
                Initialize();
                CollectionId = string.IsNullOrEmpty(CollectionId) ? collectionName : CollectionId;

                var collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId);

                var query = client.CreateDocumentQuery(collectionUri, new SqlQuerySpec()
                {
                    //QueryText = "SELECT * FROM c WHERE udf.ConverToDateTime(c.modifiedDate) > udf.ConverToDateTime(@lastUpdatedTime)",
                    QueryText = "SELECT * FROM c WHERE c.modifiedDate > @lastUpdatedTime",
                    Parameters = new SqlParameterCollection()
                    {
                        new SqlParameter("@lastUpdatedTime", lastUpdatedTime)
                    }
                }, DefaultOptions);


                var result = query.ToList();
                return result;

                //StoredProcedure storedProcedure = new StoredProcedure()
                //{
                //    Id = "TestSP",
                //    Body = "SELECT * FROM Incomes t WHERE udf.Tax(t.income) > 20000",
                //};

                //var query = new SqlQuerySpec("SELECT * FROM c WHERE c.ModifiedDate > @lastUpdatedTime", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@lastUpdatedTime", Value = null } }));

                //var sp = client.CreateStoredProcedureQuery(docCollectionUri, query);

                //await client.CreateStoredProcedureAsync(docCollectionUri, storedProcedure);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static async void CreateUDF()
        {
            var docCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId);

            try
            {
                var udfLink = $"{docCollectionUri}/udfs/{"ConverToDateTime"}";

                var result = await client.ReadUserDefinedFunctionAsync(udfLink);
                if (result.Resource != null)
                {
                    // The UDF with udfId exists

                }
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    UserDefinedFunction udf = await client.CreateUserDefinedFunctionAsync(docCollectionUri, new UserDefinedFunction()
                    {
                        Id = "ConverToDateTime",
                        Body = @"function convertTime(datetime){
                        datetime = datetime.replace(/-/g,'/')  
                        if(datetime){
                            var date = new Date(datetime);
                        }
                        return date.getDate() + date.getTime();
                        // else{
                        //     var date = new Date();
                        // }
                        // time1 = date.getTime(); 
                        // return time1;
                    }",
                    });
                }
            }
        }

        private static async Task CreateCollectionIfNotExistsAsync()
        {
            try
            {
                var docCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId);
                await client.ReadDocumentCollectionAsync(docCollectionUri);
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await client.CreateDocumentCollectionAsync(
                        UriFactory.CreateDatabaseUri(DatabaseId),
                        new DocumentCollection { Id = CollectionId },
                        new Microsoft.Azure.Documents.Client.RequestOptions { OfferThroughput = 1000 });
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
